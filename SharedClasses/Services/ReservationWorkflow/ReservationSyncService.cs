using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using System.Globalization;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow;

public interface IReservationSyncService
{
    Task<BitrixLinkBackfillBatchResultDto> BackfillBitrixLinksAsync(
        BitrixLinkBackfillRequestDto request,
        CancellationToken cancellationToken = default);
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
    public const int MaxBitrixLinkBackfillIdentifiers = 100;
    private const string BitrixLinkBackfillUpdateReason = "Controlled Bitrix reservation link backfill";
    private readonly IReservationStore _store;
    private readonly IReservationWorkflowSyncOperations _workflowSyncOperations;
    private readonly ILogger<ReservationSyncService> _logger;
    private readonly TimeProvider _timeProvider;

    public ReservationSyncService(
        IReservationStore store,
        IReservationWorkflowSyncOperations workflowSyncOperations,
        ILogger<ReservationSyncService> logger,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _workflowSyncOperations = workflowSyncOperations ?? throw new ArgumentNullException(nameof(workflowSyncOperations));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BitrixLinkBackfillBatchResultDto> BackfillBitrixLinksAsync(
        BitrixLinkBackfillRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservationGuids = (request.ReservationGuids ?? new List<Guid>())
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToList();
        var idoReservationIds = (request.IdoReservationIds ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var requestedIdentifierCount = reservationGuids.Count + idoReservationIds.Count;

        if (requestedIdentifierCount == 0)
        {
            throw new ArgumentException(
                "Provide at least one valid identifier in reservationGuids or idoReservationIds.",
                nameof(request));
        }

        if (requestedIdentifierCount > MaxBitrixLinkBackfillIdentifiers)
        {
            throw new ArgumentException(
                $"A single request can contain at most {MaxBitrixLinkBackfillIdentifiers} distinct identifiers.",
                nameof(request));
        }

        var batchResult = new BitrixLinkBackfillBatchResultDto
        {
            DryRun = request.DryRun,
            RequestedIdentifierCount = requestedIdentifierCount
        };
        var resolvedReservationGuids = new HashSet<Guid>();

        foreach (var reservationGuid in reservationGuids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var record = await _store.GetAsync(reservationGuid, cancellationToken);
                if (record is null)
                {
                    batchResult.Results.Add(CreateNotFoundResult(reservationGuid, null));
                    continue;
                }

                if (resolvedReservationGuids.Add(record.ReservationGuid))
                {
                    batchResult.Results.Add(await BackfillBitrixLinksForRecordAsync(record, request.DryRun, cancellationToken));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Bitrix link backfill identifier {ReservationGuid}.", reservationGuid);
                batchResult.Results.Add(CreateLookupFailureResult(reservationGuid, null, ex));
            }
        }

        foreach (var idoReservationId in idoReservationIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var record = await _store.GetByIdoReservationIdAsync(idoReservationId, cancellationToken);
                if (record is null)
                {
                    batchResult.Results.Add(CreateNotFoundResult(null, idoReservationId));
                    continue;
                }

                if (resolvedReservationGuids.Add(record.ReservationGuid))
                {
                    batchResult.Results.Add(await BackfillBitrixLinksForRecordAsync(record, request.DryRun, cancellationToken));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Bitrix link backfill identifier {IdoReservationId}.", idoReservationId);
                batchResult.Results.Add(CreateLookupFailureResult(null, idoReservationId, ex));
            }
        }

        return batchResult;
    }

    private async Task<BitrixLinkBackfillItemResultDto> BackfillBitrixLinksForRecordAsync(
        ReservationRecord record,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = new BitrixLinkBackfillItemResultDto
        {
            ReservationGuid = record.ReservationGuid,
            IdoReservationId = record.IdoReservationId,
            PreviousClientBitrixId = record.ClientBitrixId,
            PreviousDealBitrixId = record.DealBitrixId,
            ClientBitrixId = record.ClientBitrixId,
            DealBitrixId = record.DealBitrixId
        };

        if (record.ClientBitrixId.HasValue && record.DealBitrixId.HasValue)
        {
            result.Status = BitrixLinkBackfillStatuses.Skipped;
            result.Message = "Both Bitrix links are already populated.";
            return result;
        }

        if (!record.IdoReservationId.HasValue || record.IdoReservationId.Value <= 0)
        {
            result.Status = BitrixLinkBackfillStatuses.Skipped;
            result.Message = "Reservation does not have a valid ido_reservation_id.";
            return result;
        }

        if (record.State.Client is null)
        {
            result.Status = BitrixLinkBackfillStatuses.Skipped;
            result.Message = "Reservation state does not contain client data.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(record.State.Client.Email))
        {
            result.Status = BitrixLinkBackfillStatuses.Skipped;
            result.Message = "Reservation client does not have an email address.";
            return result;
        }

        if (record.State.StartRequest is null)
        {
            result.Status = BitrixLinkBackfillStatuses.Skipped;
            result.Message = "Reservation state does not contain StartRequest data.";
            return result;
        }

        if (!record.ClientBitrixId.HasValue)
        {
            result.Actions.Add("EnsureClientBitrixLink");
        }

        if (!record.DealBitrixId.HasValue)
        {
            result.Actions.Add("EnsureDealBitrixLink");
        }
        else if (!record.ClientBitrixId.HasValue)
        {
            result.Actions.Add("UpdateDealContactLink");
        }

        if (dryRun)
        {
            result.Status = BitrixLinkBackfillStatuses.Planned;
            result.Message = "Bitrix links would be ensured; no external or database changes were made.";
            return result;
        }

        var currentRecord = record;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shouldUpdateExistingDealContact = record.DealBitrixId.HasValue && !record.ClientBitrixId.HasValue;
            currentRecord = await _workflowSyncOperations.EnsureBitrixContactAndDealAsync(record);

            result.ClientBitrixId = currentRecord.ClientBitrixId;
            result.DealBitrixId = currentRecord.DealBitrixId;

            if (!currentRecord.ClientBitrixId.HasValue || !currentRecord.DealBitrixId.HasValue)
            {
                throw new InvalidOperationException("Bitrix link backfill completed without both required identifiers.");
            }

            if (shouldUpdateExistingDealContact)
            {
                await _workflowSyncOperations.UpdateBitrixDealAsync(currentRecord, BitrixLinkBackfillUpdateReason);
            }

            result.Status = BitrixLinkBackfillStatuses.Updated;
            result.Message = "Bitrix links were ensured and saved to reservation_records.";
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backfill Bitrix links for reservation {ReservationGuid}.", record.ReservationGuid);
            result.ClientBitrixId = currentRecord.ClientBitrixId;
            result.DealBitrixId = currentRecord.DealBitrixId;
            result.Status = BitrixLinkBackfillStatuses.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    private static BitrixLinkBackfillItemResultDto CreateNotFoundResult(Guid? reservationGuid, int? idoReservationId)
    {
        return new BitrixLinkBackfillItemResultDto
        {
            RequestedReservationGuid = reservationGuid,
            RequestedIdoReservationId = idoReservationId,
            Status = BitrixLinkBackfillStatuses.Skipped,
            Message = reservationGuid.HasValue
                ? $"No reservation_record found for reservation GUID {reservationGuid:D}."
                : $"No reservation_record found for IDO reservation id {idoReservationId}."
        };
    }

    private static BitrixLinkBackfillItemResultDto CreateLookupFailureResult(
        Guid? reservationGuid,
        int? idoReservationId,
        Exception exception)
    {
        return new BitrixLinkBackfillItemResultDto
        {
            RequestedReservationGuid = reservationGuid,
            RequestedIdoReservationId = idoReservationId,
            Status = BitrixLinkBackfillStatuses.Failed,
            Error = exception.Message
        };
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
        var syncTimestamp = _timeProvider.GetUtcNow().UtcDateTime;

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

        var syncChangeSummary = BuildReservationSyncChangeSummary(
            previousStatus,
            record.IdoStatus,
            previousApartmentId,
            record.State.StartRequest?.ObjectId);
        result.SyncChangeSummary = syncChangeSummary;

        record.SyncChangeSummary = syncChangeSummary;

        if (!dryRun)
        {
            record.LastStatusSyncAt = syncTimestamp;

            if (apartmentChanged || idoStatusChanged)
            {
                await _store.UpdateAsync(record, cancellationToken);
            }
            else
            {
                await _store.UpdateStatusSyncMetadataAsync(
                    record.ReservationGuid,
                    syncChangeSummary,
                    syncTimestamp,
                    cancellationToken);
            }
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
