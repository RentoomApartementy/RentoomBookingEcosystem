using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api;

public class AddReservationFunction
{
    private readonly IdoSellService _idoSellService;
    private readonly ILogger<AddReservationFunction> _logger;
    

    public AddReservationFunction(IdoSellService idoSellService, ILogger<AddReservationFunction> logger)
    {
        _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AddReservationToIdoSell")]
    public async Task<HttpResponseData> AddReservationToIdoSell(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/reservations")] HttpRequestData req)
    {
        _logger.LogInformation("AddReservationToIdoSell started at: {time}", DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            string body;
            using (var reader = new StreamReader(req.Body))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }
           
            var request = JsonConvert.DeserializeObject<ReservationAddParams>(body);

            if (request == null || request.Reservations.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation in the params.reservations array.");
                return response;
            }

            var result = await _idoSellService.AddReservationsAsync(request.Reservations);

            RentoomReservationHashRecord functionResult = new();
            
            foreach (var r in result.Reservations)
            {
                if (r.Success)
                {
                    functionResult = await _idoSellService.FetchReservationByIDFromIdoSellAsync(r.ReservationId.Value, true);

                }
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(functionResult));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during AddReservationToIdoSell.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("AddReservationToIdoSell finished at: {time}", DateTime.UtcNow);
        }
    }

    [Function("ChangeReservationStatusInIdoSell")]
    public async Task<HttpResponseData> ChangeReservationStatusInIdoSell(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/reservations/statuschange")] HttpRequestData req)
    {
        _logger.LogInformation("ChangeReservationStatusInIdoSell started at: {time}", DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            string body;
            using (var reader = new StreamReader(req.Body))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            var request = JsonConvert.DeserializeObject<List<EditReservationsStatusRequest>>(body);

            if (request == null || request.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation in the  array.");
                return response;
            }

            var result = await _idoSellService.ChangeReservationsStatusAsync(request);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ChangeReservationStatusInIdoSell.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("ChangeReservationStatusInIdoSell finished at: {time}", DateTime.UtcNow);
        }
    }
}