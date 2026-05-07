using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public class SyncDealCheckTimesFromReservationRecordFunction
{
    private const string CheckInTimeFieldName = "UF_CRM_1778170129465";
    private const string CheckOutTimeFieldName = "UF_CRM_1778170154231";

    private readonly ILogger<SyncDealCheckTimesFromReservationRecordFunction> _logger;
    private readonly IReservationStore _reservationStore;
    private readonly BitrixService _bitrixService;

    public SyncDealCheckTimesFromReservationRecordFunction(
        ILogger<SyncDealCheckTimesFromReservationRecordFunction> logger,
        IReservationStore reservationStore,
        BitrixService bitrixService)
    {
        _logger = logger;
        _reservationStore = reservationStore;
        _bitrixService = bitrixService;
    }

    [Function("SyncDealCheckTimesFromReservationRecord")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bitrix/deals/sync-check-times-from-reservation-record")] HttpRequest req)
    {
        var cancellationToken = req.HttpContext?.RequestAborted ?? CancellationToken.None;

        string requestBody;
        using (var reader = new StreamReader(req.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

        SyncDealCheckTimesRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SyncDealCheckTimesRequest>(
                requestBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid payload for SyncDealCheckTimesFromReservationRecord.");
            return new BadRequestObjectResult("Invalid JSON payload.");
        }

        var dealIds = payload?.DealIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (dealIds is null || dealIds.Count == 0)
        {
            return new BadRequestObjectResult("Provide at least one valid deal id in dealIds.");
        }

        var results = new List<SyncDealCheckTimesResult>();

        foreach (var dealId in dealIds)
        {
            try
            {
                var reservationRecord = await _reservationStore.GetByDealBitrixIdAsync(dealId, cancellationToken);
                if (reservationRecord is null)
                {
                    results.Add(new SyncDealCheckTimesResult
                    {
                        DealId = dealId,
                        Success = false,
                        Error = $"No reservation_record found for Bitrix deal id {dealId}."
                    });
                    continue;
                }

                var startRequest = reservationRecord.State?.StartRequest;
                if (startRequest is null)
                {
                    results.Add(new SyncDealCheckTimesResult
                    {
                        DealId = dealId,
                        ReservationGuid = reservationRecord.ReservationGuid,
                        Success = false,
                        Error = $"Reservation record {reservationRecord.ReservationGuid} has no StartRequest in state."
                    });
                    continue;
                }

                var checkInText = startRequest.CheckInTime.ToString("HH:mm");
                var checkOutText = startRequest.CheckOutTime.ToString("HH:mm");

                await _bitrixService.UpdateDealAsync(
                    dealId,
                    new Dictionary<string, object?>
                    {
                        [CheckInTimeFieldName] = checkInText,
                        [CheckOutTimeFieldName] = checkOutText
                    });

                _logger.LogInformation(
                    "Updated Bitrix deal {DealId} with check-in {CheckIn} and check-out {CheckOut} from reservation record {ReservationGuid}.",
                    dealId,
                    checkInText,
                    checkOutText,
                    reservationRecord.ReservationGuid);

                results.Add(new SyncDealCheckTimesResult
                {
                    DealId = dealId,
                    ReservationGuid = reservationRecord.ReservationGuid,
                    CheckIn = checkInText,
                    CheckOut = checkOutText,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync check times for Bitrix deal {DealId}.", dealId);
                results.Add(new SyncDealCheckTimesResult
                {
                    DealId = dealId,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return new OkObjectResult(new
        {
            requestedCount = dealIds.Count,
            updatedCount = results.Count(r => r.Success),
            failedCount = results.Count(r => !r.Success),
            fields = new
            {
                RB_Godzina_Zameldowania = CheckInTimeFieldName,
                RB_Godzina_Wymeldowania = CheckOutTimeFieldName
            },
            results
        });
    }

    private sealed class SyncDealCheckTimesRequest
    {
        public List<int> DealIds { get; set; } = new();
    }

    private sealed class SyncDealCheckTimesResult
    {
        public int DealId { get; set; }
        public Guid? ReservationGuid { get; set; }
        public string? CheckIn { get; set; }
        public string? CheckOut { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
