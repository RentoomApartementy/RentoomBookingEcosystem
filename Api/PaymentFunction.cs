using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api
{
    public class PaymentsFunction
    {
        private readonly IdoSellService _idoSellService;
        private readonly ILogger<PaymentsFunction> _logger;

        public PaymentsFunction(IdoSellService idoSellService, ILogger<PaymentsFunction> logger)
        {
            _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("AddPayments")]
        public async Task<HttpResponseData> AddPayments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/payments")] HttpRequestData req)
        {
            _logger.LogInformation("AddPayments started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var requestBody = await ReadBodyAsync(req);

                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Request body is empty.");
                    return response;
                }

                var request = JsonConvert.DeserializeObject<PaymentAddParams>(requestBody);

                if (request == null || request.Payments.Count == 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Provide at least one payment in the params.payments array.");
                    return response;
                }

                var result = await _idoSellService.AddPaymentsAsync(request.Payments);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during AddPayments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("AddPayments finished at: {time}", DateTime.UtcNow);
            }
        }

        [Function("CancelPayments")]
        public async Task<HttpResponseData> CancelPayments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/payments/cancel")] HttpRequestData req)
        {
            _logger.LogInformation("CancelPayments started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var paymentIds = await DeserializeIdsAsync(req);
                if (paymentIds.Count == 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Provide at least one payment id.");
                    return response;
                }

                var result = await _idoSellService.CancelPaymentsAsync(paymentIds);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during CancelPayments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("CancelPayments finished at: {time}", DateTime.UtcNow);
            }
        }

        [Function("ConfirmPayments")]
        public async Task<HttpResponseData> ConfirmPayments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/payments/confirm")] HttpRequestData req)
        {
            _logger.LogInformation("ConfirmPayments started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var paymentIds = await DeserializeIdsAsync(req);
                if (paymentIds.Count == 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Provide at least one payment id.");
                    return response;
                }

                var result = await _idoSellService.ConfirmPaymentsAsync(paymentIds);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during ConfirmPayments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("ConfirmPayments finished at: {time}", DateTime.UtcNow);
            }
        }

        [Function("EditPayments")]
        public async Task<HttpResponseData> EditPayments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/payments/edit")] HttpRequestData req)
        {
            _logger.LogInformation("EditPayments started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var requestBody = await ReadBodyAsync(req);
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Request body is empty.");
                    return response;
                }

                var request = JsonConvert.DeserializeObject<PaymentEditParams>(requestBody);
                if (request == null || request.Payments.Count == 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Provide at least one payment in the params.payments array.");
                    return response;
                }

                var result = await _idoSellService.EditPaymentsAsync(request.Payments);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during EditPayments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("EditPayments finished at: {time}", DateTime.UtcNow);
            }
        }

        [Function("GetPaymentForms")]
        public async Task<HttpResponseData> GetPaymentForms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ido/payments/forms")] HttpRequestData req)
        {
            _logger.LogInformation("GetPaymentForms started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var result = await _idoSellService.GetPaymentFormsAsync();

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during GetPaymentForms.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetPaymentForms finished at: {time}", DateTime.UtcNow);
            }
        }

        [Function("GetPayments")]
        public async Task<HttpResponseData> GetPayments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/payments/search")] HttpRequestData req)
        {
            _logger.LogInformation("GetPayments started at: {time}", DateTime.UtcNow);
            var response = req.CreateResponse();

            try
            {
                var requestBody = await ReadBodyAsync(req);
                var request = string.IsNullOrWhiteSpace(requestBody)
                    ? null
                    : JsonConvert.DeserializeObject<PaymentGetRequest>(requestBody);

                var result = await _idoSellService.GetPaymentsAsync(request?.Params, request?.Settings);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during GetPayments.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetPayments finished at: {time}", DateTime.UtcNow);
            }
        }

        private static async Task<string> ReadBodyAsync(HttpRequestData req)
        {
            using var reader = new StreamReader(req.Body);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        private static async Task<List<int>> DeserializeIdsAsync(HttpRequestData req)
        {
            var requestBody = await ReadBodyAsync(req);
            var request = JsonConvert.DeserializeObject<PaymentActionParams>(requestBody ?? string.Empty);
            return request?.Payments?.ConvertAll(p => p.Id) ?? new List<int>();
        }
    }
}