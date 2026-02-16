
using Microsoft.AspNetCore.Components;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;


public class IdoLocksApi
{
    private readonly IdoLocksService _idoLocksService;
    private readonly ILogger<IdoLocksApi> _logger;

    public IdoLocksApi(IdoLocksService idoLocksService, ILogger<IdoLocksApi> logger)
    {
        _idoLocksService = idoLocksService;
        _logger = logger;
    }

    [Function("GetLocks")]
    public async Task<HttpResponseData> GetLocks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locks/{reservationId}/{itemId}")] HttpRequestData req, int reservationId, int itemId)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();
        _logger.LogInformation("GetLocks started for reservationId={ReservationId}, itemId={ItemId} at {Time}", reservationId, itemId, DateTime.UtcNow);

        try
        {
            if (reservationId <= 0 || itemId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Missing reservationId or itemId.");
                return response;
            }
            var locks = await _idoLocksService.GetLocksAsync(reservationId, itemId, cancellationToken);
            if (locks == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("No locks found for the given reservationId and itemId.");
                return response;
            }
            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(locks));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locks for reservationId={ReservationId}, itemId={ItemId}", reservationId, itemId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("An error occurred while processing your request.");
            return response;
        }
        finally
        {
            _logger.LogInformation("GetLocks finished for reservationId={ReservationId}, itemId={ItemId} at {Time}", reservationId, itemId, DateTime.UtcNow);
        }
    }
}

