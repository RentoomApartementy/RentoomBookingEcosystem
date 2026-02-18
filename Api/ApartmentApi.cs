using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System.Net;


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

       

      
        
        [Function("GetAllApartmentsFromIdoSellWithLocalizationInfoAsync")]
        public async Task<HttpResponseData> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "idb/apartments/getAll")] HttpRequestData req)
        {

            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation($"GetAllApartmentObjectsFunctionNew function started at: {DateTime.Now}");

            try
            {
                //  var result  = await _idoAppartmenrService.GetAllApartmentsFromIdoSellWithLocalizationInfoAsync();
                var result = await _idoAppartmenrService.SyncApartmentsAndAmenitiesAsync();

                List<string?> regionsFilter = result.Select(r => r?.ObjectLocation?.LocalizationItem?.Region).Distinct().ToList();
                await _filtersRepository.SaveRegionsFilters(regionsFilter,_logger);

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
                _logger.LogError(ex, "Failed to retrieve apartments." + ex.Message);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error." + ex.Message);
                return response;
            }

            
        }

        //runs every 4 hours "0 0 */4 * * *"
        [Function("GetAllApartmentsFromIdoSellWithLocalizationInfoAsyncCron")]
        [Microsoft.Azure.Functions.Worker.FixedDelayRetry(5, "00:00:10")]
        public async Task GetAllApartmentsFromIdoSellWithLocalizationInfoAsyncCron(
               [TimerTrigger("%CRON_SYNC_ALL_APARTMENTS_FROM_IDB%")] TimerInfo timerInfo,
               FunctionContext context)
        {

            var cancellationToken = context.CancellationToken;

            _logger.LogInformation($"GetAllApartmentObjectsFunctionNew function started at: {DateTime.Now}");

            try
            {
                //  var result  = await _idoAppartmenrService.GetAllApartmentsFromIdoSellWithLocalizationInfoAsync();
                var result = await _idoAppartmenrService.SyncApartmentsAndAmenitiesAsync(cancellationToken);

                List<string?> regionsFilter = result.Select(r => r?.ObjectLocation?.LocalizationItem?.Region).Distinct().ToList();
                await _filtersRepository.SaveRegionsFilters(regionsFilter, _logger);

                _logger.LogInformation("Synchronized {ApartmentsCount} apartments from IdoSell. Next scheduled run: {NextRun}", result.Count, timerInfo.ScheduleStatus?.Next);
                _logger.LogInformation($"GetAllApartmentObjectsFunctionNew function finished at: {DateTime.Now}");

            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogError(invalidOperationException, "ApartmentsService is not configured for IdoBooking access.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve apartments." + ex.Message);
                throw;
            }


        }

        [Function("SeedApartmentsToPostgres")]
        public async Task<HttpResponseData> SeedApartmentsToPostgres(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "postgres/apartments/seed")] HttpRequestData req)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            try
            {
                var result = await _idoAppartmenrService.SyncApartmentsAndAmenitiesAsync(cancellationToken);

                List<string?> regionsFilter = result.Select(r => r?.ObjectLocation?.LocalizationItem?.Region).Distinct().ToList();

                await _filtersRepository.SaveRegionsFilters(regionsFilter,_logger);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed apartments to PostgreSQL.");
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
