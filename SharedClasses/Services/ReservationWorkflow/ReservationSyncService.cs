using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using System.Globalization;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow;

public interface IReservationSyncService
{
    Task FinalizeImportedReservationAsync(Guid reservationGuid, ImportedReservationFinalizationRequest request);
    Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
    Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(Guid reservationGuid, Reservation? idoReservation, CancellationToken cancellationToken = default);
    Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(ReservationRecord record, Reservation? idoReservation = null, CancellationToken cancellationToken = default);
    Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
    Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(Guid reservationGuid, Reservation? idoReservation, CancellationToken cancellationToken = default);
    Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(ReservationRecord record, Reservation? idoReservation = null, CancellationToken cancellationToken = default);
}

public interface IReservationWorkflowSyncOperations
{
    Task EnsurePaymentTotalsAsync(Guid reservationGuid, ReservationRecord record);
    Task<ReservationRecord> RequireReservationAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
    Task<ReservationRecord> EnsureBitrixContactAndDealAsync(ReservationRecord record);
    Task UpdateBitrixDealAsync(ReservationRecord record, string updateReason, Reservation? idoReservation = null);
    Task<Reservation?> FetchIdoReservationAsync(ReservationRecord record, bool refreshCache, CancellationToken cancellationToken);
    Task CreatePaidUpsellOrderAsync(ReservationRecord record, string providerTransactionId);
}

public class ReservationSyncService : IReservationSyncService
{
    private readonly IReservationStore _store;
    private readonly IReservationWorkflowSyncOperations _workflowSyncOperations;
    private readonly ILogger<ReservationSyncService> _logger;

    public ReservationSyncService(
        IReservationStore store,
        IReservationWorkflowSyncOperations workflowSyncOperations,
        ILogger<ReservationSyncService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _workflowSyncOperations = workflowSyncOperations ?? throw new ArgumentNullException(nameof(workflowSyncOperations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task FinalizeImportedReservationAsync(Guid reservationGuid, ImportedReservationFinalizationRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        while (true)
        {
            var record = await _workflowSyncOperations.RequireReservationAsync(reservationGuid);
            await _workflowSyncOperations.EnsurePaymentTotalsAsync(record.ReservationGuid, record);

            var requestedPaymentStatus = string.IsNullOrWhiteSpace(request.PaymentStatus)
                ? record.PaymentStatus
                : request.PaymentStatus;

            if (string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(requestedPaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                requestedPaymentStatus = record.PaymentStatus;
            }

            var alreadyProcessedAsPaid = string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(requestedPaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.ProviderTransactionId, request.ProviderTransactionId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Provider, request.Provider, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(request.Provider))
            {
                record.Provider = request.Provider;
            }

            if (!string.IsNullOrWhiteSpace(request.ProviderTransactionId))
            {
                record.ProviderTransactionId = request.ProviderTransactionId;
            }

            if (!string.IsNullOrWhiteSpace(request.IdoStatus))
            {
                record.IdoStatus = request.IdoStatus;
            }

            record.PaymentStatus = requestedPaymentStatus;

            try
            {
                await _store.UpdateAsync(record);
                record = await _workflowSyncOperations.EnsureBitrixContactAndDealAsync(record);

                if (string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                    && !alreadyProcessedAsPaid)
                {
                    await _workflowSyncOperations.CreatePaidUpsellOrderAsync(record, request.ProviderTransactionId);
                }

                await _workflowSyncOperations.UpdateBitrixDealAsync(
                    record,
                    string.IsNullOrWhiteSpace(request.UpdateReason)
                        ? "Imported reservation synchronized"
                        : request.UpdateReason);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while finalizing imported reservation {ReservationGuid}. Retrying.", reservationGuid);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }
    }

    public async Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
    {
        var record = await _workflowSyncOperations.RequireReservationAsync(reservationGuid, cancellationToken);
        return await SyncReservationStatusInternalAsync(record, dryRun: false, cancellationToken: cancellationToken);
    }

    public async Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(Guid reservationGuid, Reservation? idoReservation, CancellationToken cancellationToken = default)
    {
        var record = await _workflowSyncOperations.RequireReservationAsync(reservationGuid, cancellationToken);
        return await SyncReservationStatusInternalAsync(record, dryRun: false, idoReservation, cancellationToken);
    }

    public async Task<ReservationStatusSyncResultDto> SyncReservationStatusAsync(ReservationRecord record, Reservation? idoReservation = null, CancellationToken cancellationToken = default)
    {
        return await SyncReservationStatusInternalAsync(record, dryRun: false, idoReservation, cancellationToken);
    }

    public async Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
    {
        var record = await _workflowSyncOperations.RequireReservationAsync(reservationGuid, cancellationToken);
        return await SyncReservationStatusInternalAsync(record, dryRun: true, cancellationToken: cancellationToken);
    }

    public async Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(Guid reservationGuid, Reservation? idoReservation, CancellationToken cancellationToken = default)
    {
        var record = await _workflowSyncOperations.RequireReservationAsync(reservationGuid, cancellationToken);
        return await SyncReservationStatusInternalAsync(record, dryRun: true, idoReservation, cancellationToken);
    }

    public async Task<ReservationStatusSyncResultDto> PreviewReservationStatusSyncAsync(ReservationRecord record, Reservation? idoReservation = null, CancellationToken cancellationToken = default)
    {
        return await SyncReservationStatusInternalAsync(record, dryRun: true, idoReservation, cancellationToken);
    }

    private async Task<ReservationStatusSyncResultDto> SyncReservationStatusInternalAsync(
        ReservationRecord record,
        bool dryRun,
        Reservation? preloadedIdoReservation = null,
        CancellationToken cancellationToken = default)
    {
        if (!record.IdoReservationId.HasValue)
        {
            throw new InvalidOperationException($"Reservation {record.ReservationGuid} does not have IdoReservationId.");
        }

        var previousStatus = record.IdoStatus;
        var previousApartmentId = record.State.StartRequest?.ObjectId;

        var result = new ReservationStatusSyncResultDto
        {
            ReservationGuid = record.ReservationGuid,
            IdoReservationId = record.IdoReservationId,
            PreviousIdoStatus = record.IdoStatus,
            PreviousPaymentStatus = record.PaymentStatus,
            DryRun = dryRun
        };

        var currentPaymentStatus = record.PaymentStatus;

        var idoReservation = preloadedIdoReservation ?? await _workflowSyncOperations.FetchIdoReservationAsync(record, true, cancellationToken);
        var idoStatus = idoReservation?.ReservationDetails?.status;
        var reservationItem = idoReservation?.Items?.FirstOrDefault();
        var currentApartmentId = reservationItem?.objectId;
        var currentApartmentItemId = reservationItem?.objectItemId > 0
            ? reservationItem.objectItemId
            : reservationItem?.itemId;

        var apartmentChanged = false;
        if (record.State.StartRequest is not null)
        {
            if (currentApartmentId.HasValue
                && currentApartmentId.Value > 0
                && currentApartmentId.Value != record.State.StartRequest.ObjectId)
            {
                record.State.StartRequest.ObjectId = currentApartmentId.Value;
                apartmentChanged = true;
            }

            if (currentApartmentItemId.HasValue
                && currentApartmentItemId.Value > 0
                && currentApartmentItemId.Value != record.State.StartRequest.ObjectItemId)
            {
                record.State.StartRequest.ObjectItemId = currentApartmentItemId.Value;
                apartmentChanged = true;
            }
        }

        var idoStatusChanged = false;
        if (!string.IsNullOrWhiteSpace(idoStatus)
            && !string.Equals(idoStatus, record.IdoStatus, StringComparison.OrdinalIgnoreCase))
        {
            record.IdoStatus = idoStatus;
            idoStatusChanged = true;
        }

        if (!dryRun && (apartmentChanged || idoStatusChanged))
        {
            await _store.UpdateAsync(record, cancellationToken);
        }

        var syncChangeSummary = BuildReservationSyncChangeSummary(
            previousStatus,
            record.IdoStatus,
            previousApartmentId,
            record.State.StartRequest?.ObjectId);
        result.SyncChangeSummary = syncChangeSummary;

        if (!dryRun)
        {
            record.SyncChangeSummary = syncChangeSummary;
            await _store.UpdateAsync(record, cancellationToken);
        }

        if (!dryRun
            && (apartmentChanged || idoStatusChanged)
            && !record.DealBitrixId.HasValue
            && record.State.Client is not null)
        {
            record = await _workflowSyncOperations.EnsureBitrixContactAndDealAsync(record);
        }

        if (!dryRun && record.DealBitrixId.HasValue && (apartmentChanged || idoStatusChanged))
        {
            await _workflowSyncOperations.UpdateBitrixDealAsync(record, "Cron reservation status sync", idoReservation);
            result.BitrixUpdated = true;
        }

        if (dryRun)
        {
            result.CurrentIdoStatus = record.IdoStatus;
            result.CurrentPaymentStatus = currentPaymentStatus;
        }
        else
        {
            record = await _workflowSyncOperations.RequireReservationAsync(record.ReservationGuid, cancellationToken);
            result.CurrentIdoStatus = record.IdoStatus;
            result.CurrentPaymentStatus = record.PaymentStatus;
        }

        return result;
    }
    private static string BuildReservationSyncChangeSummary(
        string? previousStatus,
        string? currentStatus,
        int? previousApartmentId,
        int? currentApartmentId)
    {
        var statusPart = string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase)
            ? "Status: nochange"
            : $"Status: {DisplayValue(previousStatus)} -> {DisplayValue(currentStatus)}";

        var apartmentPart = previousApartmentId == currentApartmentId
            ? "Apartment: nochange"
            : $"Apartment: {DisplayValue(previousApartmentId)} -> {DisplayValue(currentApartmentId)}";

        return $"{statusPart}; {apartmentPart}";
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "null" : value;
    }

    private static string DisplayValue(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
    }
}
