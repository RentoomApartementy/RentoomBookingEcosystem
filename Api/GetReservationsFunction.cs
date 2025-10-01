using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Services;

namespace RentoomBooking.Api;

public class GetReservationsFunction
{

    private readonly IdoSellService _bookingObjectService;

    private readonly ILogger<GetReservationsFunction> _logger;

  

    public GetReservationsFunction(ILogger<GetReservationsFunction> logger, IdoSellService bookingObjectService)
    {
        _logger = logger;
        _bookingObjectService = bookingObjectService;
    }

    [Function("GetReservationsById")]
    public async Task<HttpResponseData> GetReservationById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservations/{reservationId}")]
    HttpRequestData req,
        int? reservationId)
    {
        _logger.LogInformation("GetReservationById started at: {time}", DateTime.UtcNow);

        var res = req.CreateResponse();

        try
        {
            var id = reservationId
                     ?? TryParseQuery(req, "reservationId");

            if (id is null)
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationId in path (/reservations/{id}), query (?reservationId=).");
                return res;
            }

            var reservation = await _bookingObjectService.FetchReservationByIDFromIdoSellAsync(id.Value);

            if (reservation == null || reservation.result == null)
            {
                res.StatusCode = System.Net.HttpStatusCode.NotFound;
                await res.WriteStringAsync($"Reservation with id {id.Value} not found.");
                return res;
            }

            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(System.Text.Json.JsonSerializer.Serialize(reservation));
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetReservationsById.");
            res.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.");
            return res;
        }
        finally
        {
            _logger.LogInformation("GetReservationById finished at: {time}", DateTime.UtcNow);
        }
    }

    private static int? TryParseQuery(HttpRequestData req, string key)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        return int.TryParse(query.Get(key), out var v) ? v : (int?)null;
    }

}