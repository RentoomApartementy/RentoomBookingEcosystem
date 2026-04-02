using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.Net;

namespace RentoomBooking.Api;

public class ReservationStatusSyncCronFunction
{
    private readonly ILogger<ReservationStatusSyncCronFunction> _logger;
    private readonly IReservationStore _reservationStore;
    private readonly IReservationWorkflowService _reservationWorkflowService;

    public ReservationStatusSyncCronFunction(
        ILogger<ReservationStatusSyncCronFunction> logger,
        IReservationStore reservationStore,
        IReservationWorkflowService reservationWorkflowService)
    {
        _logger = logger;
        _reservationStore = reservationStore;
        _reservationWorkflowService = reservationWorkflowService;
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

    private async Task<SyncSummary> ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        var records = await _reservationStore.ListActiveWithIdoReservationAsync(cancellationToken);
        var results = new List<object>();
        var succeededCount = 0;
        var failedCount = 0;

        foreach (var record in records)
        {
            try
            {
                var syncResult = await _reservationWorkflowService.SyncReservationStatusAsync(record.ReservationGuid, cancellationToken);
                succeededCount++;

                _logger.LogInformation(
                    "Synchronized reservation {ReservationGuid} / {IdoReservationId}. IdoStatus: {PreviousIdoStatus} -> {CurrentIdoStatus}. PaymentStatus: {PreviousPaymentStatus} -> {CurrentPaymentStatus}. TpayChecked={TpayChecked}. TpayFinalStatusApplied={TpayFinalStatusApplied}. BitrixUpdated={BitrixUpdated}. Warning={Warning}",
                    syncResult.ReservationGuid,
                    syncResult.IdoReservationId,
                    syncResult.PreviousIdoStatus,
                    syncResult.CurrentIdoStatus,
                    syncResult.PreviousPaymentStatus,
                    syncResult.CurrentPaymentStatus,
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

        return new SyncSummary
        {
            ProcessedCount = records.Count,
            SucceededCount = succeededCount,
            FailedCount = failedCount,
            Results = results
        };
    }

    private sealed class SyncSummary
    {
        public int ProcessedCount { get; set; }
        public int SucceededCount { get; set; }
        public int FailedCount { get; set; }
        public List<object> Results { get; set; } = new();
    }
}
