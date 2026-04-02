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

        foreach (var batch in records.Chunk(BatchSize))
        {
            var idoReservations = await FetchBatchReservationsAsync(batch, dryRun, cancellationToken);

            foreach (var record in batch)
            {
                try
                {
                    idoReservations.TryGetValue(record.IdoReservationId!.Value, out var idoReservation);

                    var syncResult = dryRun
                        ? await _reservationSyncService.PreviewReservationStatusSyncAsync(record, idoReservation, cancellationToken)
                        : await _reservationSyncService.SyncReservationStatusAsync(record, idoReservation, cancellationToken);
                    succeededCount++;

                    _logger.LogInformation(
                        "{Mode} reservation sync for {ReservationGuid} / {IdoReservationId}. IdoStatus: {PreviousIdoStatus} -> {CurrentIdoStatus}. PaymentStatus: {PreviousPaymentStatus} -> {CurrentPaymentStatus}. Changes={SyncChangeSummary}. TpayChecked={TpayChecked}. TpayFinalStatusApplied={TpayFinalStatusApplied}. BitrixUpdated={BitrixUpdated}. Warning={Warning}",
                        dryRun ? "Previewed" : "Synchronized",
                        syncResult.ReservationGuid,
                        syncResult.IdoReservationId,
                        syncResult.PreviousIdoStatus,
                        syncResult.CurrentIdoStatus,
                        syncResult.PreviousPaymentStatus,
                        syncResult.CurrentPaymentStatus,
                        syncResult.SyncChangeSummary,
                        syncResult.TpayChecked,
                        syncResult.TpayFinalStatusApplied,
                        syncResult.BitrixUpdated,
                        syncResult.Warning);

                    results.Add(syncResult);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(
                        ex,
                        "Failed to synchronize reservation {ReservationGuid} / {IdoReservationId}.",
                        record.ReservationGuid,
                        record.IdoReservationId);

                    results.Add(new
                    {
                        reservationGuid = record.ReservationGuid,
                        idoReservationId = record.IdoReservationId,
                        success = false,
                        error = ex.Message
                    });
                }
            }
        }

        return new SyncSummary
        {
            ProcessedCount = records.Count,
            SucceededCount = succeededCount,
            FailedCount = failedCount,
            Results = results
        };
    }

    private async Task<IReadOnlyDictionary<int, Reservation>> FetchBatchReservationsAsync(
        IEnumerable<ReservationRecord> batch,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var batchList = batch
            .Where(record => record.IdoReservationId.HasValue)
            .ToList();

        if (batchList.Count == 0)
        {
            return new Dictionary<int, Reservation>();
        }

        var idoReservationIds = batchList
            .Select(record => record.IdoReservationId!.Value)
            .Distinct()
            .ToList();

        var reservationTokensById = batchList
            .GroupBy(record => record.IdoReservationId!.Value)
            .ToDictionary(
                group => group.Key,
                group => dryRun ? null : group.First().ReservationGuid.ToString("D"));

        _logger.LogInformation(
            "Fetching IdoBooking reservations in batch. Count={Count}, FirstReservationId={FirstReservationId}, LastReservationId={LastReservationId}, DryRun={DryRun}.",
            idoReservationIds.Count,
            idoReservationIds.First(),
            idoReservationIds.Last(),
            dryRun);

        return await _idoSellService.FetchReservationsByIDsFromIdoSellAsync(
            idoReservationIds,
            saveToDb: !dryRun,
            reservationTokensById,
            cancellationToken);
    }

    private sealed class SyncSummary
    {
        public int ProcessedCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public List<object> Results { get; set; } = new();
    }
}
