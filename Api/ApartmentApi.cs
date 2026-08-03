using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;
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
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "idb/apartments/getAll")] HttpRequestData req,
                [DurableClient] DurableTaskClient durableTaskClient)
        {
            var response = req.CreateResponse();
            try
            {
                var instanceId = await IdoBookingSyncStarter.StartOrGetActiveAsync(durableTaskClient, req.FunctionContext.CancellationToken);
                response.StatusCode = HttpStatusCode.Accepted;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    instanceId,
                    statusUrl = $"{req.Url.Scheme}://{req.Url.Authority}/api/idb/apartments/sync/{instanceId}",
                    currentStatusUrl = $"{req.Url.Scheme}://{req.Url.Authority}/api/idb/apartments/sync/current"
                }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start durable IdoBooking synchronization.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Unable to start apartment synchronization.");
                return response;
            }
        }

        //runs every 4 hours "0 0 */4 * * *"
        [Function("GetAllApartmentsFromIdoSellWithLocalizationInfoAsyncCron")]
        [Microsoft.Azure.Functions.Worker.FixedDelayRetry(5, "00:00:10")]
        public async Task GetAllApartmentsFromIdoSellWithLocalizationInfoAsyncCron(
               [TimerTrigger("%CRON_SYNC_ALL_APARTMENTS_FROM_IDB%")] TimerInfo timerInfo,
               FunctionContext context,
               [DurableClient] DurableTaskClient durableTaskClient)
        {
            try
            {
                var instanceId = await IdoBookingSyncStarter.StartOrGetActiveAsync(durableTaskClient, context.CancellationToken);
                _logger.LogInformation("IdoBooking sync is scheduled or already active. InstanceId={InstanceId}, NextScheduledRun={NextRun}", instanceId, timerInfo.ScheduleStatus?.Next);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to schedule IdoBooking durable synchronization.");
                throw;
            }
        }

        [Function("SeedApartmentsToPostgres")]
        public async Task<HttpResponseData> SeedApartmentsToPostgres(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "postgres/apartments/seed")] HttpRequestData req,
            [DurableClient] DurableTaskClient durableTaskClient)
        {
            return await GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(req, durableTaskClient);
        }

        [Function("GetIdoBookingApartmentSyncStatus")]
        public async Task<HttpResponseData> GetIdoBookingApartmentSyncStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "idb/apartments/sync/{instanceId}")] HttpRequestData req,
            string instanceId,
            [DurableClient] DurableTaskClient durableTaskClient)
        {
            var metadata = await durableTaskClient.GetInstanceAsync(instanceId, getInputsAndOutputs: true, req.FunctionContext.CancellationToken);
            var response = req.CreateResponse(metadata is null ? HttpStatusCode.NotFound : HttpStatusCode.OK);
            if (metadata is null)
            {
                await response.WriteStringAsync("Synchronization instance was not found.");
                return response;
            }

            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(new
            {
                instanceId = metadata.InstanceId,
                runtimeStatus = metadata.RuntimeStatus.ToString(),
                createdAtUtc = metadata.CreatedAt,
                lastUpdatedAtUtc = metadata.LastUpdatedAt,
                customStatus = metadata.ReadCustomStatusAs<IdoBookingSyncStatus>(),
                result = metadata.ReadOutputAs<IdoBookingSyncResult>()
            }));
            return response;
        }

        [Function("GetCurrentIdoBookingApartmentSyncStatus")]
        public async Task<HttpResponseData> GetCurrentIdoBookingApartmentSyncStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "idb/apartments/sync/current")] HttpRequestData req,
            [DurableClient] DurableTaskClient durableTaskClient)
        {
            var instanceId = await IdoBookingSyncStarter.GetActiveInstanceIdAsync(durableTaskClient, req.FunctionContext.CancellationToken);
            if (instanceId is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("No IdoBooking synchronization is active.");
                return notFound;
            }

            return await GetIdoBookingApartmentSyncStatus(req, instanceId, durableTaskClient);
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
