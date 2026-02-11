using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Services;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp
{
    public class TTLockCodeApi
    {
        private readonly RappQrMaintService _qrMaintService;
        private readonly ILogger<TTLockCodeApi> _logger;

        public TTLockCodeApi(RappQrMaintService qrMaintService, ILogger<TTLockCodeApi> logger)
        {
            _qrMaintService = qrMaintService;
            _logger = logger;
        }

        [Function("GetLockCode")]
        public async Task<HttpResponseData> GetLockCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lockcode/{apartmentItemId:int}")] HttpRequestData req, int apartmentItemId)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("GetLockCode started for apartmentItemId={ApartmentItemId} at {Time}", apartmentItemId, DateTime.UtcNow);

            try
            {
                if (apartmentItemId <= 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing or invalid apartmentItemId.");
                    return response;
                }

                var lockCode = await _qrMaintService.GetLockCodeAsync(apartmentItemId, cancellationToken);
                if (string.IsNullOrEmpty(lockCode))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No lock code found for the given apartmentItemId.");
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new { lockCode }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lock code for apartmentItemId={ApartmentItemId}", apartmentItemId);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred while processing your request.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetLockCode finished for apartmentItemId={ApartmentItemId} at {Time}", apartmentItemId, DateTime.UtcNow);
            }
        }
    }
}