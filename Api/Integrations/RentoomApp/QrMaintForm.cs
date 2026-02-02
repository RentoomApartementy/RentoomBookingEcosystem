using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp
{
    public class QrMaintApi
    {
        private readonly RappQrMaintService _qrMaintService;
        private readonly ILogger<QrMaintApi> _logger;

        public QrMaintApi(RappQrMaintService qrMaintService, ILogger<QrMaintApi> logger)
        {
            _qrMaintService = qrMaintService;
            _logger = logger;
        }

        [Function("GetQrMaintFormUrl")]
        public async Task<HttpResponseData> GetQrMaintFormUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "qrmaint/form-url/{apartmentId:int}")] HttpRequestData req, int apartmentId)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("GetQrMaintFormUrl started for apartmentId={ApartmentId} at {Time}", apartmentId, DateTime.UtcNow);

            try
            {
                if (apartmentId <= 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing or invalid apartmentId.");
                    return response;
                }

                var url = await _qrMaintService.GetQrMaintFormUrlAsync(apartmentId, cancellationToken);
                if (string.IsNullOrEmpty(url))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No QRMaint form URL found for the given apartmentId.");
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new { url }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching QRMaint form URL for apartmentId={ApartmentId}", apartmentId);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred while processing your request.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetQrMaintFormUrl finished for apartmentId={ApartmentId} at {Time}", apartmentId, DateTime.UtcNow);
            }
        }

    }
}
