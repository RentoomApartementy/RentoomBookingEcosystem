using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;

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
    public async Task Run(
        [TimerTrigger("%CRON_SYNC_ACTIVE_RESERVATION_STATUSES%")] TimerInfo timerInfo,
        FunctionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var records = await _reservationStore.ListActiveWithIdoReservationAsync(cancellationToken);

        var syncedCount = 0;
        var failedCount = 0;

        _logger.LogInformation(
            "Starting active reservation status sync for {Count} reservation records. Next scheduled run: {NextRun}",
            records.Count,
            timerInfo.ScheduleStatus?.Next);

        foreach (var record in records)
        {
            try
            {
                var syncResult = await _reservationWorkflowService.SyncReservationStatusAsync(record.ReservationGuid, cancellationToken);
                syncedCount++;

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
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(
                    ex,
                    "Failed to synchronize reservation {ReservationGuid} / {IdoReservationId}.",
                    record.ReservationGuid,
                    record.IdoReservationId);
            }
        }

        _logger.LogInformation(
            "Finished active reservation status sync. Processed={Processed}, Succeeded={Succeeded}, Failed={Failed}.",
            records.Count,
            syncedCount,
            failedCount);
    }
}
