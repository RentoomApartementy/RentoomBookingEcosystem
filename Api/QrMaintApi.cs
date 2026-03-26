using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using System.Net;

namespace RentoomBooking.Api
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

        [Function("GetQrMaintWifiInfo")]
        public async Task<HttpResponseData> GetQrMaintWifiInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "qrmaint/wifi/{apartmentItemId:int}")]
            HttpRequestData req,
            int apartmentItemId)
        {
            _logger.LogInformation("GetQrMaintWifiInfo started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            if (apartmentItemId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid apartmentItemId.");
                return response;
            }

            var wifiInfo = await _qrMaintService.GetWifiInfoAsync(apartmentItemId, req.FunctionContext.CancellationToken);
            if (wifiInfo == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Wi-Fi data not found.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(wifiInfo));
            return response;
        }
    }
}