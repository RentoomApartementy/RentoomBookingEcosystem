using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;

namespace RentoomBooking.Api;

public class GetReservationsFunction
{

    private readonly IdoSellService _bookingObjectService;
    private readonly PostgresBookingDatabase _bookingDatabase;
    private readonly ILogger<GetReservationsFunction> _logger;



    public GetReservationsFunction(ILogger<GetReservationsFunction> logger, IdoSellService bookingObjectService, PostgresBookingDatabase bookingDatabase)
    {
        _logger = logger;
        _bookingObjectService = bookingObjectService;
        _bookingDatabase = bookingDatabase;
    }

    [Function("GetReservationsByIdFromIdoBooking")]
    public async Task<HttpResponseData> GetReservationsByIdFromIdoBooking(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ido/reservations/{reservationId:int?}/{save:bool?}")]
    HttpRequestData req,
        int? reservationId, bool? save)
    {
        _logger.LogInformation("GetReservationById started at: {time}", DateTime.UtcNow);

        var res = req.CreateResponse();

        try
        {
            var id = reservationId;
            var saveToDb = save ?? false;

            if (id is null)
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationId in path (/reservations/{id}), query (?reservationId=).");
                return res;
            }

            var ret = await _bookingObjectService.FetchReservationByIDFromIdoSellAsync(id.Value, saveToDb);

            if (ret.ReservationResponse.result.Reservations == null)
            {
                res.StatusCode = System.Net.HttpStatusCode.NotFound;
                await res.WriteStringAsync($"Reservation with id {id.Value} not found.");
                return res;
            }

            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");

            await res.WriteStringAsync(JsonConvert.SerializeObject(ret));

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

     
    [Function("GetReservationsByTokenFromDb")]
    public async Task<HttpResponseData> GetReservationsByIdFromDb(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/reservations/{reservationToken}")] HttpRequestData req, string? reservationToken)
    {
        _logger.LogInformation("GetReservationsByIdFromDb started at: {time}", DateTime.UtcNow);
        var res = req.CreateResponse();
        try
        {
            var token = reservationToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationToken in path (/reservations/{reservationToken}), query (?reservationToken=).");
                return res;
            }
            var ret = await _bookingDatabase.GetRentoomReservationByResTokenAsync(token, _logger);
            if (ret == null)
            {
                res.StatusCode = System.Net.HttpStatusCode.NotFound;
                await res.WriteStringAsync($"Reservation with token {token} not found in database.");
                return res;
            }


            // Check if ToDate is before today
            var toDate = ret.Reservation?.ReservationDetails?.getDateTo();
            if (toDate != null && toDate.Value.Date < DateTime.UtcNow.Date)
            {
                res.StatusCode = System.Net.HttpStatusCode.Gone;
                await res.WriteStringAsync($"Reservation with token {token} has expired (ToDate: {toDate:yyyy-MM-dd}).");
                return res;
            }

            var resStatus = ret.Reservation?.ReservationDetails?.status;

            if (resStatus != ReservationStatusType.Accepted)
            {
                res.StatusCode = System.Net.HttpStatusCode.UnprocessableContent;
                await res.WriteStringAsync($"Reservation with token {token} is not accessible (Status: {resStatus}).");
                return res;
            }


            



            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(ret));
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetReservationsByIdFromDb.");
            res.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.");
            return res;
        }
        finally
        {
            _logger.LogInformation("GetReservationsByIdFromDb finished at: {time}", DateTime.UtcNow);
        }
    }  

}