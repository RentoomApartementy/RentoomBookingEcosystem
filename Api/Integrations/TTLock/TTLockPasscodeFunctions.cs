using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.TTLock.Services;
using System.Net;

namespace RentoomBooking.Api.Integrations.TTLock
{
    public class TTLockPasscodeFunctions
    {
        private readonly ITTLockPasscodeAppService _ttLockPasscodeAppService;
        private readonly ILogger<TTLockPasscodeFunctions> _logger;

        public TTLockPasscodeFunctions(
            ITTLockPasscodeAppService ttLockPasscodeAppService,
            ILogger<TTLockPasscodeFunctions> logger)
        {
            _ttLockPasscodeAppService = ttLockPasscodeAppService;
            _logger = logger;
        }

        [Function("GetAccessCodes")]
        public async Task<HttpResponseData> GetAccessCodes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservation/{reservationToken}/access-codes")]
            HttpRequestData req,
            string reservationToken)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
                    return response;
                }

                var result = await _ttLockPasscodeAppService.GetAccessCodesAsync(reservationToken, ct);
                response.StatusCode = result.StatusCode;
                if (result.Payload is not null)
                {
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(JsonConvert.SerializeObject(result.Payload));
                }
                else
                {
                    await response.WriteStringAsync(result.ErrorMessage ?? "An error occurred.");
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAccessCodes for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }

        [Function("GenerateAccessCode")]
        public async Task<HttpResponseData> GenerateAccessCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservation/{reservationToken}/access-codes/generate")]
            HttpRequestData req,
            string reservationToken)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation("GenerateAccessCode started for token={Token}", reservationToken);

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
                    return response;
                }

                var result = await _ttLockPasscodeAppService.GenerateAccessCodeAsync(reservationToken, ct);
                response.StatusCode = result.StatusCode;
                if (result.Payload is not null)
                {
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(JsonConvert.SerializeObject(result.Payload));
                }
                else
                {
                    await response.WriteStringAsync(result.ErrorMessage ?? "An error occurred.");
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateAccessCode for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }
    }
}
