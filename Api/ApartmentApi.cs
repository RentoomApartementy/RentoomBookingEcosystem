using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api
{
    public class ApartmentApi
    {

        private readonly ILogger<ApartmentApi> _logger;
        private readonly IIdoApartmentService _idoAppartmenrService;
        private readonly IApartmentsService _apartmentsService;
        private readonly FiltersRepository _filtersRepository;

        public ApartmentApi(IIdoApartmentService idoAppartmenrService, IApartmentsService apartmentsService, FiltersRepository filtersRepository, ILogger<ApartmentApi> logger)
        {

            _logger = logger;
            _idoAppartmenrService = idoAppartmenrService;
            _apartmentsService = apartmentsService;
            _filtersRepository = filtersRepository;
        }

        /* do usunięcia - funkcja nie potrzebna, bo nie zwraca sama sobie nic uzytecznego. a lokacja jest teraz w ApartmentObject
        [Function("GetApartmentLocations")]
        public async Task<HttpResponseData> GetApartmentLocations(
                [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "apartments/locations")] HttpRequestData req)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            try
            {
                ParamsSearchObjectLocationType? parameters = null;

                var requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    try
                    {
                        var payload = JsonConvert.DeserializeObject<ObjectLocationRequestPayloadInternal>(requestBody);
                        parameters = payload?.ParamsSearchObjectLocation;

                        if (parameters == null)
                        {
                            parameters = JsonConvert.DeserializeObject<ParamsSearchObjectLocationType>(requestBody);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Invalid apartment location payload.");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync("Invalid JSON payload.");
                        return response;
                    }
                }

                var result = await _apartmentsService.GetObjectLocationsAsync(parameters, cancellationToken);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogError(invalidOperationException, "ApartmentsService is not configured for IdoBooking access.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Apartment service configuration error.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve apartment locations.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
        }
        */

      
        
        [Function("GetAllApartmentsFromIdoSellWithLocalizationInfoAsync")]
        public async Task<HttpResponseData> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartments/getAll")] HttpRequestData req)
        {

            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation($"GetAllApartmentObjectsFunctionNew function started at: {DateTime.Now}");

            try
            {
                //  var result  = await _idoAppartmenrService.GetAllApartmentsFromIdoSellWithLocalizationInfoAsync();
                var result = await _idoAppartmenrService.SyncApartmentsAndAmenitiesAsync();

                List<string?> regionsFilter = result.Select(r => r?.ObjectLocation?.LocalizationItem?.Region).ToList();

                await _filtersRepository.SaveRegionsFilters(regionsFilter);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                _logger.LogInformation($"GetAllApartmentObjectsFunctionNew function finished at: {DateTime.Now}");
                return response;
                

            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogError(invalidOperationException, "ApartmentsService is not configured for IdoBooking access.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Apartment service configuration error.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve apartments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }

            
        }

        [Function("GetApartmentByIdAsync")]
        public async Task<HttpResponseData> GetApartmentByIdAsync(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/apartments/{id}")] HttpRequestData req,
    int id)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation($"GetApartmentByIdAsync function started at: {DateTime.Now} for Id: {id}");

            try
            {
                var result = await _apartmentsService.GetApartmentByIdAsync(id);

                if (result == null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync($"Apartment with Id {id} not found in local repository.");
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                _logger.LogInformation($"GetApartmentByIdAsync function finished at: {DateTime.Now} for Id: {id}");
                return response;
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogError(invalidOperationException, "ApartmentsService is not configured for IdoBooking access.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Apartment service configuration error.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve apartment with Id: {id}.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
        }


    }
    }
