using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;

namespace RentoomBooking.Api;

public class GetReservationUpsellsFunction
{
    private readonly IUpsellPurchasedSummaryService _upsellPurchasedSummaryService;
    private readonly ILogger<GetReservationUpsellsFunction> _logger;

    public GetReservationUpsellsFunction(IUpsellPurchasedSummaryService upsellPurchasedSummaryService, ILogger<GetReservationUpsellsFunction> logger)
    {
        _upsellPurchasedSummaryService = upsellPurchasedSummaryService ?? throw new ArgumentNullException(nameof(upsellPurchasedSummaryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetReservationUpsellsByToken")]
    public async Task<HttpResponseData> GetReservationUpsellsByToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/reservations/{reservationToken}/upsells")] HttpRequestData req,
        string reservationToken,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetReservationUpsellsByToken started at: {time}", DateTime.UtcNow);
        var res = req.CreateResponse();

        try
        {
            if (string.IsNullOrWhiteSpace(reservationToken))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationToken in path (/reservations/{reservationToken}/upsells).", cancellationToken);
                return res;
            }

            if (!Guid.TryParse(reservationToken, out var reservationGuid))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Reservation token must be a valid GUID.", cancellationToken);
                return res;
            }

            var responseDto = await _upsellPurchasedSummaryService.GetPurchasedSummaryAsync(reservationGuid, cancellationToken);

            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(responseDto), cancellationToken);
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetReservationUpsellsByToken.");
            res.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.", cancellationToken);
            return res;
        }
        finally
        {
            _logger.LogInformation("GetReservationUpsellsByToken finished at: {time}", DateTime.UtcNow);
        }
    }
}
