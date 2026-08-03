using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBooking.Api;

public sealed record IdoBookingSyncApartment(int ApartmentId, string? Region);
public sealed record IdoBookingSyncStatus(
    string RunId,
    string State,
    int TotalApartments,
    int CompletedApartments,
    int FailedApartments,
    int? LastApartmentId,
    IReadOnlyList<string> Errors,
    DateTime UpdatedAtUtc);
public sealed record IdoBookingSyncResult(string RunId, string State, int TotalApartments, int CompletedApartments, IReadOnlyList<string> Errors);
public sealed class IdoBookingSyncActivities
{
    private readonly IIdoApartmentService _idoApartmentService;
    private readonly FiltersRepository _filtersRepository;
    private readonly ILogger<IdoBookingSyncActivities> _logger;

    public IdoBookingSyncActivities(
        IIdoApartmentService idoApartmentService,
        FiltersRepository filtersRepository,
        ILogger<IdoBookingSyncActivities> logger)
    {
        _idoApartmentService = idoApartmentService;
        _filtersRepository = filtersRepository;
        _logger = logger;
    }

    [Function(nameof(FetchAndSaveApartmentsActivity))]
    public async Task<List<IdoBookingSyncApartment>> FetchAndSaveApartmentsActivity([ActivityTrigger] object? _, FunctionContext context)
    {
        var apartments = await _idoApartmentService.SaveAllApartmentsToPostgresAsync(context.CancellationToken);
        return apartments.Select(x => new IdoBookingSyncApartment(x.Id, x.ObjectLocation?.LocalizationItem?.Region)).ToList();
    }

    [Function(nameof(SyncApartmentActivity))]
    public async Task SyncApartmentActivity([ActivityTrigger] int apartmentId, FunctionContext context)
    {
        _logger.LogInformation("Synchronizing apartment {ApartmentId} amenities and media.", apartmentId);
        await _idoApartmentService.SyncApartmentAmenitiesAsync(apartmentId, context.CancellationToken);
        await _idoApartmentService.SyncApartmentMediaAssetsAsync(apartmentId, context.CancellationToken);
    }

    [Function(nameof(SaveRegionsActivity))]
    public Task SaveRegionsActivity([ActivityTrigger] List<string?> regions, FunctionContext context) =>
        _filtersRepository.SaveRegionsFilters(regions, _logger);
}

public static class IdoBookingSyncOrchestrator
{
    public const string FunctionName = nameof(IdoBookingSyncOrchestrator);
    public const int MaxConcurrentApartments = 4;

    [Function(FunctionName)]
    public static async Task<IdoBookingSyncResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(FunctionName);
        var runId = context.InstanceId;
        var errors = new List<string>();
        var retry = TaskOptions.FromRetryPolicy(new RetryPolicy(3, TimeSpan.FromSeconds(10)));

        var apartments = await context.CallActivityAsync<List<IdoBookingSyncApartment>>(nameof(IdoBookingSyncActivities.FetchAndSaveApartmentsActivity), new object(), retry);
        context.SetCustomStatus(new IdoBookingSyncStatus(runId, "running", apartments.Count, 0, 0, null, errors, context.CurrentUtcDateTime));

        var completed = 0;
        for (var offset = 0; offset < apartments.Count; offset += MaxConcurrentApartments)
        {
            var batch = apartments.Skip(offset).Take(MaxConcurrentApartments).ToList();
            var tasks = batch.Select(apartment => context.CallActivityAsync(nameof(IdoBookingSyncActivities.SyncApartmentActivity), apartment.ApartmentId, retry)).ToList();

            for (var index = 0; index < tasks.Count; index++)
            {
                var apartment = batch[index];
                try
                {
                    await tasks[index];
                    completed++;
                }
                catch (Exception ex)
                {
                    var message = $"Apartment {apartment.ApartmentId}: {ex.GetBaseException().Message}";
                    errors.Add(message.Length > 500 ? message[..500] : message);
                    logger.LogError(ex, "Apartment {ApartmentId} failed after Durable retry policy. RunId={RunId}", apartment.ApartmentId, runId);
                }

                context.SetCustomStatus(new IdoBookingSyncStatus(
                    runId,
                    "running",
                    apartments.Count,
                    completed,
                    errors.Count,
                    apartment.ApartmentId,
                    errors,
                    context.CurrentUtcDateTime));
            }
        }

        await context.CallActivityAsync(nameof(IdoBookingSyncActivities.SaveRegionsActivity), apartments.Select(x => x.Region).Distinct().ToList(), retry);
        var state = errors.Count == 0 ? "completed" : "completed_with_errors";
        var result = new IdoBookingSyncResult(runId, state, apartments.Count, completed, errors);
        context.SetCustomStatus(new IdoBookingSyncStatus(runId, state, apartments.Count, completed, errors.Count, null, errors, context.CurrentUtcDateTime));
        return result;
    }
}

public static class IdoBookingSyncStarter
{
    public static async Task<string?> GetActiveInstanceIdAsync(DurableTaskClient client, CancellationToken cancellationToken)
    {
        const string instanceId = "ido-booking-apartments-sync";
        var metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);
        return metadata is not null && metadata.RuntimeStatus is OrchestrationRuntimeStatus.Pending or OrchestrationRuntimeStatus.Running ? instanceId : null;
    }

    public static async Task<string> StartOrGetActiveAsync(DurableTaskClient client, CancellationToken cancellationToken)
    {
        const string instanceId = "ido-booking-apartments-sync";
        var existing = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: false, cancellationToken);
        if (existing is not null && existing.RuntimeStatus is OrchestrationRuntimeStatus.Pending or OrchestrationRuntimeStatus.Running)
        {
            return instanceId;
        }

        if (existing is not null)
        {
            await client.PurgeInstanceAsync(instanceId, cancellationToken);
        }

        return await client.ScheduleNewOrchestrationInstanceAsync(
            IdoBookingSyncOrchestrator.FunctionName,
            new StartOrchestrationOptions { InstanceId = instanceId },
            cancellationToken);
    }
}

public static class IdoBookingSyncHistoryCleanup
{
    [Function(nameof(IdoBookingSyncHistoryCleanup))]
    public static async Task Run(
        [TimerTrigger("0 0 3 * * *")] TimerInfo _,
        [DurableClient] DurableTaskClient durableTaskClient,
        FunctionContext context)
    {
        var result = await durableTaskClient.PurgeInstancesAsync(
            createdFrom: null,
            createdTo: DateTimeOffset.UtcNow.AddDays(-30),
            statuses: new[]
            {
                OrchestrationRuntimeStatus.Completed,
                OrchestrationRuntimeStatus.Failed,
                OrchestrationRuntimeStatus.Terminated,
                OrchestrationRuntimeStatus.Canceled
            },
            context.CancellationToken);

        context.GetLogger(nameof(IdoBookingSyncHistoryCleanup))
            .LogInformation("Purged {Count} Durable orchestration histories older than 30 days.", result.PurgedInstanceCount);
    }
}
