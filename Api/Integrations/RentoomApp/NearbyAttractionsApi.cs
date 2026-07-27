using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp;

public class NearbyAttractionsApi
{
    private readonly ApartmentNearbyAttractionsService _service;
    private readonly ILogger<NearbyAttractionsApi> _logger;

    public NearbyAttractionsApi(ApartmentNearbyAttractionsService service, ILogger<NearbyAttractionsApi> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("GetNearbyAttractionsForApartment")]
    public async Task<HttpResponseData> GetNearbyAttractions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "apartments/{objectId:int}/nearby-attractions")] HttpRequestData req,
        int objectId)
    {
        var ct = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();

        if (objectId <= 0)
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("objectId must be a positive integer.", ct);
            return response;
        }

        try
        {
            var result = await _service.GetNearbyAttractionsByObjectIdAsync(objectId, ct)
                         ?? new NearbyAttractionsResultDTO();

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result), ct); // camelCase (DefaultSettings)
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyAttractions failed for objectId={ObjectId}", objectId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", ct);
            return response;
        }
    }
}
