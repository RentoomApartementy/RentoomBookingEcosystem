using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System;
using System.Globalization;
using System.Net;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locks/{reservationId}/{itemId}")] HttpRequestData req,
        string reservationId,
        string itemId)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();
        _logger.LogInformation("GetLocks started for reservationId={ReservationId}, itemId={ItemId} at {Time}", reservationId, itemId, DateTime.UtcNow);

        try
        {
            if (!int.TryParse(reservationId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resId) ||
                !int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itmId) ||
                resId <= 0 || itmId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Missing or invalid reservationId or itemId.");
                return response;
            }

            var locks = await _idoLocksService.GetLocksAsync(resId, itmId, cancellationToken);
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

