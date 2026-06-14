using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.Net;

namespace RentoomBooking.Api;

public class ReservationStatusSyncCronFunction
{
    private const int MaxConcurrentFetchAndSyncWorkers = 4;
    private readonly ILogger<ReservationStatusSyncCronFunction> _logger;
    private readonly IReservationStore _reservationStore;
    private readonly IReservationSyncService _reservationSyncService;
    private readonly IdoSellService _idoSellService;
    private const int BatchSize = 50;

    public ReservationStatusSyncCronFunction(
        ILogger<ReservationStatusSyncCronFunction> logger,
        IReservationStore reservationStore,
        IReservationSyncService reservationSyncService,
        IdoSellService idoSellService)
    {
        _logger = logger;
        _reservationStore = reservationStore;
        _reservationSyncService = reservationSyncService;
        _idoSellService = idoSellService;
    }

    [Function("SyncActiveReservationStatusesCron")]
    [FixedDelayRetry(5, "00:00:10")]
    public async Task RunCron(
        [TimerTrigger("%CRON_SYNC_ACTIVE_RESERVATION_STATUSES%")] TimerInfo timerInfo,
        FunctionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var summary = await ExecuteSyncAsync(cancellationToken);

        _logger.LogInformation(
            "Starting active reservation status sync for {Count} reservation records. Next scheduled run: {NextRun}",
            summary.ProcessedCount,
            timerInfo.ScheduleStatus?.Next);

        _logger.LogInformation(
            "Finished active reservation status sync. Processed={Processed}, Succeeded={Succeeded}, Failed={Failed}.",
            summary.ProcessedCount,
            summary.SucceededCount,
            summary.FailedCount);
    }

    [Function("SyncActiveReservationStatuses")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "reservations/statuses/sync-active")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var summary = await ExecuteSyncAsync(cancellationToken);

        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new
        {
            processedCount = summary.ProcessedCount,
            succeededCount = summary.SucceededCount,
            failedCount = summary.FailedCount,
            results = summary.Results
        }), cancellationToken);

        return response;
    }

    [Function("PreviewActiveReservationStatuses")]
    public async Task<HttpResponseData> RunPreviewHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "reservations/statuses/sync-active/preview")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var summary = await ExecuteSyncAsync(cancellationToken, dryRun: true);

        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonConvert.SerializeObject(new
        {
            dryRun = true,
            processedCount = summary.ProcessedCount,
            succeededCount = summary.SucceededCount,
            failedCount = summary.FailedCount,
            results = summary.Results
        }), cancellationToken);

        return response;
    }

    private async Task<SyncSummary> ExecuteSyncAsync(CancellationToken cancellationToken, bool dryRun = false)
    {
        var records = await _reservationStore.ListActiveWithIdoReservationAsync(cancellationToken);
        var results = new List<object>();
        var succeededCount = 0;
        var failedCount = 0;
        var resultsLock = new object();

        _logger.LogInformation(
            "Starting reservation status sync with single-reservation IdoBooking fetches because the batch fetch endpoint is unreliable. BatchSize={BatchSize}, MaxConcurrentFetchAndSyncWorkers={MaxConcurrentFetchAndSyncWorkers}, DryRun={DryRun}.",
            BatchSize,
            MaxConcurrentFetchAndSyncWorkers,
            dryRun);

        foreach (var batch in records.Chunk(BatchSize))
        {
            using var concurrencyLimiter = new SemaphoreSlim(MaxConcurrentFetchAndSyncWorkers);
            var batchTasks = batch.Select(async record =>
            {
                await concurrencyLimiter.WaitAsync(cancellationToken);

                try
                {
                    var idoReservation = await FetchSingleReservationAsync(record, dryRun, cancellationToken);

                    var syncResult = dryRun
                        ? await _reservationSyncService.PreviewReservationStatusSyncAsync(record, idoReservation, cancellationToken)
                        : await _reservationSyncService.SyncReservationStatusAsync(record, idoReservation, cancellationToken);

                    lock (resultsLock)
                    {
                        succeededCount++;
                        results.Add(syncResult);
                    }

                    _logger.LogInformation(
                        "{Mode} reservation sync for {ReservationGuid} / {IdoReservationId}. IdoStatus: {PreviousIdoStatus} -> {CurrentIdoStatus}. PaymentStatus: {PreviousPaymentStatus} -> {CurrentPaymentStatus}. Changes={SyncChangeSummary}. BitrixUpdated={BitrixUpdated}. Warning={Warning}",
                        dryRun ? "Previewed" : "Synchronized",
                        syncResult.ReservationGuid,
                        syncResult.IdoReservationId,
                        syncResult.PreviousIdoStatus,
                        syncResult.CurrentIdoStatus,
                        syncResult.PreviousPaymentStatus,
                        syncResult.CurrentPaymentStatus,
                        syncResult.SyncChangeSummary,
                        syncResult.BitrixUpdated,
                        syncResult.Warning);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to synchronize reservation {ReservationGuid} / {IdoReservationId}.",
                        record.ReservationGuid,
                        record.IdoReservationId);

                    lock (resultsLock)
                    {
                        failedCount++;
                        results.Add(new
                        {
                            reservationGuid = record.ReservationGuid,
                            idoReservationId = record.IdoReservationId,
                            success = false,
                            error = ex.Message
                        });
                    }
                }
                finally
                {
                    concurrencyLimiter.Release();
                }
            });

            await Task.WhenAll(batchTasks);
        }

        _logger.LogInformation(
            "Completed reservation status sync with single-reservation IdoBooking fetches. Processed={Processed}, Succeeded={Succeeded}, Failed={Failed}, MaxConcurrentFetchAndSyncWorkers={MaxConcurrentFetchAndSyncWorkers}.",
            records.Count,
            succeededCount,
            failedCount,
            MaxConcurrentFetchAndSyncWorkers);

        return new SyncSummary
        {
            ProcessedCount = records.Count,
            SucceededCount = succeededCount,
            FailedCount = failedCount,
            Results = results
        };
    }

    private async Task<Reservation?> FetchSingleReservationAsync(
        ReservationRecord record,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (!record.IdoReservationId.HasValue)
        {
            return null;
        }

        _logger.LogInformation(
            "Fetching single IdoBooking reservation for sync. ReservationGuid={ReservationGuid}, IdoReservationId={IdoReservationId}, DryRun={DryRun}.",
            record.ReservationGuid,
            record.IdoReservationId,
            dryRun);

        var response = await _idoSellService.FetchReservationByIDFromIdoSellAsync(
            record.IdoReservationId.Value,
            saveToDb: !dryRun,
            existingResToken: dryRun ? null : record.ReservationGuid.ToString("D"),
            cancellationToken);

        return response?.ReservationResponse?.result?.Reservations?.FirstOrDefault();
    }

    private sealed class SyncSummary
    {
        public int ProcessedCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public List<object> Results { get; set; } = new();
    }
}
