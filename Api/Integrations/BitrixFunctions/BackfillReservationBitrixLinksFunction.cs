using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public sealed class BackfillReservationBitrixLinksFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<BackfillReservationBitrixLinksFunction> _logger;
    private readonly IReservationSyncService _reservationSyncService;

    public BackfillReservationBitrixLinksFunction(
        ILogger<BackfillReservationBitrixLinksFunction> logger,
        IReservationSyncService reservationSyncService)
    {
        _logger = logger;
        _reservationSyncService = reservationSyncService;
    }

    [Function("BackfillReservationBitrixLinks")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bitrix/reservations/backfill-links")] HttpRequest req)
    {
        var cancellationToken = req.HttpContext?.RequestAborted ?? CancellationToken.None;

        BitrixLinkBackfillRequestDto? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<BitrixLinkBackfillRequestDto>(
                req.Body,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload for controlled Bitrix reservation backfill.");
            return new BadRequestObjectResult(new
            {
                error = "Invalid JSON payload."
            });
        }

        if (request is null)
        {
            return new BadRequestObjectResult(new
            {
                error = "Request body is required."
            });
        }

        try
        {
            var result = await _reservationSyncService.BackfillBitrixLinksAsync(request, cancellationToken);

            _logger.LogInformation(
                "Controlled Bitrix reservation backfill finished. DryRun={DryRun}, RequestedIdentifiers={RequestedIdentifierCount}, Resolved={ResolvedRecordCount}, Planned={PlannedCount}, Updated={UpdatedCount}, Skipped={SkippedCount}, Failed={FailedCount}.",
                result.DryRun,
                result.RequestedIdentifierCount,
                result.ResolvedRecordCount,
                result.PlannedCount,
                result.UpdatedCount,
                result.SkippedCount,
                result.FailedCount);

            return new OkObjectResult(result);
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new
            {
                error = ex.Message
            });
        }
    }
}
