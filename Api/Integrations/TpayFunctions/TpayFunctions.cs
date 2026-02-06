using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.Payments;
using RentoomBooking.SharedClasses.Services.Payments;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.TpayFunctions;

    public class TpayFunctions
    {
        private readonly ITpayNotificationValidator _validator;
        private readonly IPaymentOrchestrator _paymentOrchestrator;
        private readonly TpaySettings _settings;
        private readonly ILogger<TpayFunctions> _logger;

        public TpayFunctions(
            ITpayNotificationValidator validator,
            IPaymentOrchestrator paymentOrchestrator,
            IOptions<TpaySettings> options,
            ILogger<TpayFunctions> logger)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _paymentOrchestrator = paymentOrchestrator ?? throw new ArgumentNullException(nameof(paymentOrchestrator));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("TpayCreateTransaction")]
        public async Task<HttpResponseData> CreateTransactionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tpay/create")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            var body = await ReadBodyAsync(req);
            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            TpayCreatePaymentRequest? request;
            PaymentIntentRequest? intent;
        
            try
            {
                request = JsonConvert.DeserializeObject<TpayCreatePaymentRequest>(body);
            }
            catch (JsonException)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.");
                return response;
            }

            if (request is null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid payment request.");
                return response;
            }

            intent = new PaymentIntentRequest
            {
                FlowType = request.FlowType,
                OrderId = request.OrderId == Guid.Empty ? null : request.OrderId,
                SuccessUrl = request.SuccessUrl,
                ErrorUrl = request.ErrorUrl,
                UpsellOrder = request.UpsellOrder
            };

            if (intent.UpsellOrder is not null)
            {
                if (string.IsNullOrWhiteSpace(intent.UpsellOrder.Buyer.Email) && !string.IsNullOrWhiteSpace(request.Email))
                {
                    intent.UpsellOrder.Buyer.Email = request.Email;
                }

                if (string.IsNullOrWhiteSpace(intent.UpsellOrder.Buyer.Name) && !string.IsNullOrWhiteSpace(request.Name))
                {
                    intent.UpsellOrder.Buyer.Name = request.Name;
                }
            }

            if (intent.FlowType == PaymentFlowType.Reservation && (!intent.OrderId.HasValue || intent.OrderId == Guid.Empty))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Reservation payments require OrderId.");
                return response;
            }

            if (intent.FlowType == PaymentFlowType.Upsell && intent.UpsellOrder is null && (!intent.OrderId.HasValue || intent.OrderId == Guid.Empty))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Upsell payments require UpsellOrder or existing OrderId.");
                return response;
            }

            var paymentSession = await _paymentOrchestrator.CreatePaymentAsync(intent, req.FunctionContext.CancellationToken);

            var payload = new TpayCreatePaymentResponse
            {
                TransactionId = paymentSession.ProviderTransactionId,
                TransactionPaymentUrl = paymentSession.RedirectUrl,
                PaymentSessionGuid = paymentSession.PaymentSessionGuid
            };

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(payload));
            return response;
        }

        [Function("TpayNotification")]
        public async Task<HttpResponseData> HandleNotificationAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tpay/notification")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        
        
        var payload = await ReadBodyAsync(req);


        _logger.LogWarning("TPay webhook received payload={payload}", payload);
       

        _logger.LogWarning("TPay webhook with settings ={settigs}", JsonConvert.SerializeObject(_settings));

        

        var hasSignatureHeader = req.Headers.TryGetValues("X-JWS-Signature", out var sigValues);
            var signature = hasSignatureHeader ? sigValues.FirstOrDefault() : null;

        _logger.LogWarning("TPay webhook received signature={sig}", signature);

        var certValid = true; //await _validator.ValidateJwsAsync(signature, payload, req.FunctionContext.CancellationToken);

        if (!certValid)
            {
            _logger.LogWarning("TPay webhook invalid JWS. HasHeader={HasHeader}. SignaturePresent={SignaturePresent}.",hasSignatureHeader,!string.IsNullOrWhiteSpace(signature));

            response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync($"FALSE - TPay webhook invalid JWS. HasHeader={hasSignatureHeader}. SignaturePresent={!string.IsNullOrWhiteSpace(signature)}.");
                return response;
            }

            var form = QueryHelpers.ParseQuery(payload);
            var notification = new TpayTransactionSettlementNotification
            {
                id = form.TryGetValue("id", out var merchantId) ? merchantId.ToString() : null,
                tr_id = form.TryGetValue("tr_id", out var trId) ? trId.ToString() : null,
                tr_date = form.TryGetValue("tr_date", out var trDate) ? trDate.ToString() : null,
                tr_crc = form.TryGetValue("tr_crc", out var trCrc) ? trCrc.ToString() : null,
                tr_amount = form.TryGetValue("tr_amount", out var trAmount) ? trAmount.ToString() : null,
                tr_paid = form.TryGetValue("tr_paid", out var trPaid) ? trPaid.ToString() : null,
                tr_desc = form.TryGetValue("tr_desc", out var trDesc) ? trDesc.ToString() : null,
                tr_status = form.TryGetValue("tr_status", out var trStatus) ? trStatus.ToString() : null,
                tr_error = form.TryGetValue("tr_error", out var trError) ? trError.ToString() : null,
                tr_email = form.TryGetValue("tr_email", out var trEmail) ? trEmail.ToString() : null,
                md5sum = form.TryGetValue("md5sum", out var md5) ? md5.ToString() : null,
                test_mode = form.TryGetValue("test_mode", out var testMode) ? testMode.ToString() : null,
                card_token = form.TryGetValue("card_token", out var cardToken) ? cardToken.ToString() : null,
                token_expiry_date = form.TryGetValue("token_expiry_date", out var tokenExpiry) ? tokenExpiry.ToString() : null,
                card_tail = form.TryGetValue("card_tail", out var cardTail) ? cardTail.ToString() : null,
                card_brand = form.TryGetValue("card_brand", out var cardBrand) ? cardBrand.ToString() : null,
            };

           if(false)// if (!_validator.ValidateMd5(notification))
            {
                _logger.LogWarning("TPay webhook invalid md5. tr_id={TransactionId}, tr_crc={Crc}", notification.tr_id, notification.tr_crc);


                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync($"FALSE - TPay webhook invalid md5. tr_id={notification.tr_id}, tr_crc={notification.tr_crc}");
                return response;
            }

            if (string.IsNullOrWhiteSpace(notification.tr_id))
            {
                _logger.LogWarning("Tpay notification missing tr_id.");
                await response.WriteStringAsync("FALSE - pay notification missing tr_id.");
                return response;
            }

            var isSuccess = string.Equals(notification.tr_status, "true", StringComparison.OrdinalIgnoreCase);
            if (!isSuccess)
            {
                _logger.LogInformation("Received non-success Tpay notification for transaction {TransactionId}: {Status}", notification.tr_id, notification.tr_status);
                await response.WriteStringAsync($"TRUE - Received non-success Tpay notification for transaction {notification.tr_id}: {notification.tr_status}");
                return response;
            }

            var handledResult = await _paymentOrchestrator.HandleTpayWebhookAsync(notification.tr_id, "PAID", req.FunctionContext.CancellationToken);
            if (!handledResult.Handled)
            {
                _logger.LogWarning("No payment found for Tpay transaction {TransactionId}.", notification.tr_id);
                await response.WriteStringAsync($"TRUE - No payment found for Tpay transaction {notification.tr_id}");
                return response;
            }

            await response.WriteStringAsync("TRUE");
            return response;
        }

        private static async Task<string> ReadBodyAsync(HttpRequestData req)
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
    }
