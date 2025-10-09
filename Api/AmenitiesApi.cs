using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api;

public class AmenitiesApi
{
    private readonly IdoSellService _service;
    private readonly ILogger<AmenitiesApi> _logger;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
  //  private readonly AmenitiesRepository _amenitiesRepository;
    private readonly IAmenitiesService _amenitiesService;
    public AmenitiesApi(IdoSellService service, ILogger<AmenitiesApi> logger, IAmenitiesService amenitiesService)
    {
        _service = service;
        _logger = logger;
      //_amenitiesRepository = amenitiesRepository;
        _amenitiesService = amenitiesService;
    }

    [Function("GetAmenitiesForObjectTypes")]
    public async Task<HttpResponseData> GetAmenitiesForObjectTypes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "amenities/getForObjects")] HttpRequestData req)
    {
        var response = req.CreateResponse();

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var objectTypesQuery = queryParams.Get("objectTypesIds");

            List<IdoBookingObjectType> objectTypes = new();

            if (!string.IsNullOrWhiteSpace(objectTypesQuery))
            {
                foreach (var token in objectTypesQuery.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim();
                    if (int.TryParse(trimmed, out var id) && Enum.IsDefined(typeof(IdoBookingObjectType), id))
                    {
                        objectTypes.Add((IdoBookingObjectType)id);
                    }
                    else if (Enum.TryParse(trimmed, true, out IdoBookingObjectType enumValue))
                    {
                        objectTypes.Add(enumValue);
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync($"Invalid objectTypesIds value: {trimmed}.");
                        return response;
                    }
                }
            }

            var result = await _service.FetchAmenitiesForObjectTypesAsync(objectTypes);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result ?? new List<ObjectTypesAmenities>()));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching amenities for object types");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }

    [Function("GetAmenitiesFilter")]
    public async Task<HttpResponseData> GetAmenitiesFilter(
         [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "amenities/getForObjects/filter")] HttpRequestData req)
    {
        var response = req.CreateResponse();

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var objectTypesQuery = queryParams.Get("objectTypesIds");

            List<IdoBookingObjectType> objectTypes = new();

            if (!string.IsNullOrWhiteSpace(objectTypesQuery))
            {
                foreach (var token in objectTypesQuery.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim();
                    if (int.TryParse(trimmed, out var id) && Enum.IsDefined(typeof(IdoBookingObjectType), id))
                    {
                        objectTypes.Add((IdoBookingObjectType)id);
                    }
                    else if (Enum.TryParse(trimmed, true, out IdoBookingObjectType enumValue))
                    {
                        objectTypes.Add(enumValue);
                    }
                    else
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync($"Invalid objectTypesIds value: {trimmed}.");
                        return response;
                    }
                }
            }

            var result = await _amenitiesService.GetFilteredAmenitiesForObjectTypes(objectTypes);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result ?? new List<ObjectTypesAmenities>()));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching amenities for object types");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }

    }
