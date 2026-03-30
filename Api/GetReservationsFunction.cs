using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;

namespace RentoomBooking.Api;

public class GetReservationsFunction
{

    private readonly IdoSellService _bookingObjectService;
    private readonly IReservationStore _reservationStore;
    private readonly IReservationWorkflowService _reservationWorkflowService;
    private readonly ILogger<GetReservationsFunction> _logger;



    public GetReservationsFunction(ILogger<GetReservationsFunction> logger, IdoSellService bookingObjectService, IReservationStore reservationStore, IReservationWorkflowService reservationWorkflowService)
    {
        _logger = logger;
        _bookingObjectService = bookingObjectService;
        _reservationStore = reservationStore;
        _reservationWorkflowService = reservationWorkflowService;
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
            var cancellationToken = req.FunctionContext.CancellationToken;
            ReservationRecord? reservationRecord = null;
            Guid reservationGuid = Guid.Empty;
           if (string.IsNullOrWhiteSpace(token))
            {
                res.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide reservationToken in path (/reservations/{reservationToken}), query (?reservationToken=).");
                return res;
            }

            if (Guid.TryParse(token, out reservationGuid))
            {
                reservationRecord = await _reservationStore.GetAsync(reservationGuid, cancellationToken);
               /* if (reservationRecord is not null && !string.Equals(reservationRecord.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                {
                    res.StatusCode = System.Net.HttpStatusCode.PaymentRequired;
                    await res.WriteStringAsync("Payment Required");
                    return res;
                }
               */
            }

            var ret = await _reservationWorkflowService.EnsureRentoomReservationByResTokenAsync(token, cancellationToken);
            if (ret == null)
            {
                res.StatusCode = System.Net.HttpStatusCode.NotFound;
                await res.WriteStringAsync($"Reservation with token {token} not found in database.");
                return res;
            }

            reservationRecord = reservationGuid != Guid.Empty
                ? await _reservationStore.GetAsync(reservationGuid, cancellationToken)
                : await _reservationStore.GetByIdoReservationIdAsync(ret.Reservation?.id ?? ret.Id, cancellationToken);

          /*  if (reservationRecord is not null && !string.Equals(reservationRecord.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = System.Net.HttpStatusCode.PaymentRequired;
                await res.WriteStringAsync("Payment Required");
                return res;
            }
          */


          


            // Check if ToDate is before today
            var toDate = ret.Reservation?.ReservationDetails?.getDateTo();
            if (toDate != null && toDate.Value.Date < DateTime.UtcNow.Date)
            {
                res.StatusCode = System.Net.HttpStatusCode.Gone;
                await res.WriteStringAsync($"Reservation with token {token} has expired (ToDate: {toDate:yyyy-MM-dd}).");
                return res;
            }

            var resStatus = ret.Reservation?.ReservationDetails?.status;


            if (resStatus != ReservationStatusType.Accepted && resStatus != ReservationStatusType.Canceled)
            {
                res.StatusCode = System.Net.HttpStatusCode.UnprocessableContent;
                await res.WriteStringAsync($"Reservation with token {token} is not accessible (Status: {resStatus}).");
                return res;
            }


            



            res.StatusCode = System.Net.HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(new StayWellReservationLookupResponse
            {
                Reservation = ret,
                ReservationRecord = reservationRecord is null ? null : StayWellReservationRecordDto.FromRecord(reservationRecord)
            }));
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
