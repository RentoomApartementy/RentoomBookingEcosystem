using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api;

public class ApartmentsApi
{
    private readonly IdoSellService _service;
    private readonly ILogger<ApartmentsApi> _logger;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApartmentsApi(IdoSellService service, ILogger<ApartmentsApi> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET /api/apartments?city=Gdansk&top=50&continuationToken=...
    [Function("ListApartments")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartments")] HttpRequestData req)
    {
        var q = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var city = q.Get("city");
        var token = q.Get("continuationToken");
        int.TryParse(q.Get("top"), out var top);
        top = top is > 0 and <= 200 ? top : 50;

        _logger.LogInformation("List apartments city={City} top={Top}", city, top);

        var result = await _service.QueryApartmentsAsync(token, top);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(result,"application/json; charset=utf-8");
        return resp;
    }


    [Function("GetApartmentMedia")]
    public async Task<HttpResponseData> Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartments/{objectId:int}/media")] HttpRequestData req,
      int objectId)
    {
        _logger.LogInformation("GetApartmentMedia started for objectId={ObjectId} at {Time}", objectId, DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            if (objectId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("ObjectId is missing.");
                return response;
            }

            List<ObjectMedium>? media = await _service.FetchObjectMediaFromIdoSellAsync(objectId);

            if (media == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync($"Media not found for objectId {objectId}.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(media));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching media for objectId={ObjectId}", objectId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("GetApartmentMedia finished for objectId={ObjectId} at {Time}", objectId, DateTime.UtcNow);
        }
    }


    [Function("GetApartmentAmenities")]
    public async Task<HttpResponseData> GetAmenitiesForObjects(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartments/{objectId:int}/amenities")] HttpRequestData req,
      int objectId)
    {
        _logger.LogInformation("GetApartmentAmenities started for objectId={ObjectId} at {Time}", objectId, DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            if (objectId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("ObjectId is missing.");
                return response;
            }

            List<ObjectAmenity>? media = await _service.FetchObjectAmenitiesAsync(objectId);

            if (media == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync($"Amenities not found for objectId {objectId}.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(media));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching amenities for objectId={ObjectId}", objectId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("GetApartmentAmenities finished for objectId={ObjectId} at {Time}", objectId, DateTime.UtcNow);
        }
    }



}
