using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using System.Globalization;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public class BulkUpdateDealStayTimesFunction
{
    private const string CheckInFieldName = "UF_CRM_1773256016575";
    private const string CheckOutFieldName = "UF_CRM_1773310028374";

    private readonly ILogger<BulkUpdateDealStayTimesFunction> _logger;
    private readonly BitrixService _bitrixService;

    public BulkUpdateDealStayTimesFunction(
        ILogger<BulkUpdateDealStayTimesFunction> logger,
        BitrixService bitrixService)
    {
        _logger = logger;
        _bitrixService = bitrixService;
    }

    [Function("BulkUpdateDealStayTimes")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bitrix/deals/update-stay-times")] HttpRequest req)
    {
        string requestBody;
        using (var reader = new StreamReader(req.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

        BulkUpdateDealStayTimesRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BulkUpdateDealStayTimesRequest>(
                requestBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid payload for BulkUpdateDealStayTimes.");
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

        var results = new List<DealStayTimeUpdateResult>();

        foreach (var dealId in dealIds)
        {
            try
            {
                var rawFields = await _bitrixService.GetDealRawFieldsAsync(
                    dealId,
                    CheckInFieldName,
                    CheckOutFieldName);

                var currentCheckIn = rawFields.GetValueOrDefault(CheckInFieldName);
                var currentCheckOut = rawFields.GetValueOrDefault(CheckOutFieldName);

                var updatedCheckIn = ReplaceTimeComponent(currentCheckIn, 15, 0);
                var updatedCheckOut = ReplaceTimeComponent(currentCheckOut, 11, 0);

                await _bitrixService.UpdateDealAsync(
                    dealId,
                    new Dictionary<string, object?>
                    {
                        [CheckInFieldName] = updatedCheckIn,
                        [CheckOutFieldName] = updatedCheckOut
                    });

                results.Add(new DealStayTimeUpdateResult
                {
                    DealId = dealId,
                    Success = true,
                    PreviousCheckIn = currentCheckIn,
                    UpdatedCheckIn = updatedCheckIn,
                    PreviousCheckOut = currentCheckOut,
                    UpdatedCheckOut = updatedCheckOut
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update stay time fields for Bitrix deal {DealId}.", dealId);

                results.Add(new DealStayTimeUpdateResult
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
            updatedCount = results.Count(result => result.Success),
            failedCount = results.Count(result => !result.Success),
            results
        });
    }

    private static string ReplaceTimeComponent(string? rawValue, int hour, int minute)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException("Bitrix deal field is empty.");
        }

        if (!DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new InvalidOperationException($"Bitrix deal field value '{rawValue}' is not a valid datetime.");
        }

        var updated = new DateTimeOffset(
            parsed.Year,
            parsed.Month,
            parsed.Day,
            hour,
            minute,
            0,
            new TimeSpan(2,0,0));

        return updated.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private sealed class BulkUpdateDealStayTimesRequest
    {
        public List<int> DealIds { get; set; } = new();
    }

    private sealed class DealStayTimeUpdateResult
    {
        public int DealId { get; set; }
        public bool Success { get; set; }
        public string? PreviousCheckIn { get; set; }
        public string? UpdatedCheckIn { get; set; }
        public string? PreviousCheckOut { get; set; }
        public string? UpdatedCheckOut { get; set; }
        public string? Error { get; set; }
    }
}
