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


        /*
         *   //rb_data_wymeldunek
                ["UF_CRM_1778790948473"] = record.State.StartRequest?.EndDate.ToString("yyyy-MM-dd") + " " + record.State.StartRequest?.CheckOutTime.ToString("HH:mm"),

                //RB_Zastosowany_Bonus
                ["UF_CRM_1778175040438"] = record.State.StartRequest != null && record.State.StartRequest.AppliedBonusId.HasValue
                    ? $"{record.State.StartRequest.AppliedBonusName} ({record.State.StartRequest.DiscountAmountPln} zł, {record.State.StartRequest.AppliedBonusValue}{(record.State.StartRequest.AppliedBonusValueType == BonusDiscountValueType.Percent ? "%" : "PLN")})"
                    : "None"

         ["UF_CRM_1773256016575"] = ToBitrixDateTime(startRequest?.StartDate, startRequest?.CheckInTime, bitrixServerUTCOffset, differenceInHours),
                    ["UF_CRM_1773310028374"] = ToBitrixDateTime(startRequest?.EndDate, startRequest?.CheckOutTime, bitrixServerUTCOffset, differenceInHours),


        */


        foreach (var dealId in dealIds)
        {
            try
            {
                var rawFields = await _bitrixService.GetDealRawFieldsAsync(
                    dealId,
                    CheckInFieldName,
                    CheckOutFieldName,
                    "UF_CRM_1778790948473", //wymeldunek
                    "UF_CRM_1778790928572", //meldunek
                    "UF_CRM_1773256016575", //start-date
                    "UF_CRM_1773310028374",  //end-date
                    "UF_CRM_1768835603310" //link url do staywell

                    );

                var currentCheckIn = rawFields.GetValueOrDefault(CheckInFieldName);
                var currentCheckOut = rawFields.GetValueOrDefault(CheckOutFieldName);

                var currentStartDate = rawFields.GetValueOrDefault(CheckInFieldName);
                var currentEndDate = rawFields.GetValueOrDefault(CheckOutFieldName);

                var updatedCheckIn = ReplaceTimeComponent(currentCheckIn, 15, 0);
                var updatedCheckOut = ReplaceTimeComponent(currentCheckOut, 11, 0);

                //var updatedCheckIn = ReplaceTimeComponent(currentCheckIn, 15, 0);
                //var updatedCheckOut = ReplaceTimeComponent(currentCheckOut, 11, 0

                var updatedStartDateTimeToString =  currentStartDate?.Substring(0, 10) + " 15:00";
                var updatedEndDateTimeToString = currentEndDate?.Substring(0, 10) + " 11:00";

                var StaywellLink = rawFields.GetValueOrDefault("UF_CRM_1768835603310");

                await _bitrixService.UpdateDealAsync(
                    dealId,
                    new Dictionary<string, object?>
                    {
                       
                       // ["UF_CRM_1778790928572"] = updatedStartDateTimeToString,
                       // ["UF_CRM_1778790948473"] = updatedEndDateTimeToString,
                        ["UF_CRM_1780004115483"] = StaywellLink,
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
