using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.TpayFunctions;

    public class TpayFunctions
    {
        private readonly ITpayClient _tpayClient;
        private readonly ITpayNotificationValidator _validator;
        private readonly IReservationStore _reservationStore;
        private readonly IReservationWorkflowService _workflowService;
        private readonly IdoSellService _idoSellService;
        private readonly TpaySettings _settings;
        private readonly ILogger<TpayFunctions> _logger;

        public TpayFunctions(
            ITpayClient tpayClient,
            ITpayNotificationValidator validator,
            IReservationStore reservationStore,
            IReservationWorkflowService workflowService,
            IdoSellService idoSellService,
            IOptions<TpaySettings> options,
            ILogger<TpayFunctions> logger)
        {
            _tpayClient = tpayClient ?? throw new ArgumentNullException(nameof(tpayClient));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _reservationStore = reservationStore ?? throw new ArgumentNullException(nameof(reservationStore));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
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

            if (request is null || request.OrderId == Guid.Empty || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Email))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Missing required fields (orderId, amount, email).");
                return response;
            }

            var record = await _reservationStore.GetAsync(request.OrderId);
            if (record is null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Order not found.");
                return response;
            }

            var sessionGuid = record.PaymentSessionGuid ?? Guid.NewGuid();
            var currency = record.State.StartRequest?.Currency ?? _settings.DefaultCurrency;
            var payerName = string.IsNullOrWhiteSpace(request.Name)
                ? string.Join(" ", new[] { record.State.Client?.FirstName, record.State.Client?.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : request.Name;

            var createRequest = new TpayTransactionRequest
            {
                Amount = request.Amount,
                Currency = string.IsNullOrWhiteSpace(currency) ? _settings.DefaultCurrency : currency,
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? $"Reservation {record.ReservationGuid}"
                    : request.Description,
                Payer = new TpayPayer
                {
                    Email = request.Email,
                    Name = string.IsNullOrWhiteSpace(payerName) ? request.Email : payerName
                },
                SuccessUrl = request.SuccessUrl ?? _settings.SuccessUrl,
                ErrorUrl = request.ErrorUrl ?? _settings.ErrorUrl,
                NotificationUrl = _settings.NotificationUrl,
                HiddenDescription = record.ReservationGuid.ToString()
            };

            if (string.IsNullOrWhiteSpace(createRequest.NotificationUrl))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("NotificationUrl is not configured.");
                return response;
            }

            var tpayResult = await _tpayClient.CreateTransactionAsync(createRequest, req.FunctionContext.CancellationToken);
            if (!tpayResult.Success || string.IsNullOrWhiteSpace(tpayResult.TransactionId) || string.IsNullOrWhiteSpace(tpayResult.RedirectUrl))
            {
                _logger.LogWarning("Failed to create Tpay transaction for order {OrderId}: {Message}", request.OrderId, tpayResult.Message);
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(tpayResult.Message ?? "Failed to create payment.");
                return response;
            }

            record.PaymentSessionGuid = sessionGuid;
            record.PaymentStatus = PaymentStatuses.Initiated;
            record.Provider = record.Provider ?? "TPAY";
            record.ProviderTransactionId = tpayResult.TransactionId;
            record.State.PaymentRedirectUrl = tpayResult.RedirectUrl;
            record.IdoStatus = ReservationStatusType.WaitingForPayment;

            await _reservationStore.UpdateAsync(record);

            if (record.IdoReservationId.HasValue)
            {
                try
                {
                    var statusRequest = new EditReservationsStatusRequest
                    {
                        ReservationId = record.IdoReservationId.Value,
                        Status = ReservationStatusType.WaitingForPayment,
                        Notify = ReservationNotifyType.No,
                        NotifyService = ReservationNotifyType.No
                    };
                    await _idoSellService.ChangeReservationStatusAsync(statusRequest);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update IdoSell status for reservation {ReservationGuid}.", record.ReservationGuid);
                }
            }

        var parsed = JsonConvert.DeserializeObject<TpayTransactionCreatedResponse>(tpayResult.RawResponse);

        var payload = new TpayCreatePaymentResponse
            {
                TransactionId = tpayResult.TransactionId!,
                TransactionPaymentUrl = tpayResult.RedirectUrl!,
                PaymentSessionGuid = sessionGuid,
                TpayFullResponse = parsed
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

            if (!_validator.ValidateMd5(notification))
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

            var record = await _reservationStore.GetByProviderTransactionIdAsync(notification.tr_id);
            if (record is null)
            {
                _logger.LogWarning("No reservation found for Tpay transaction {TransactionId}.", notification.tr_id);
                await response.WriteStringAsync($"TRUE - No reservation found for Tpay transaction {notification.tr_id}");
                return response;
            }

            if (string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Duplicate notification for already paid transaction {TransactionId}.", notification.tr_id);
                await response.WriteStringAsync($"TRUE - Duplicate notification for already paid transaction {notification.tr_id}");
                return response;
            }

            var isSuccess = string.Equals(notification.tr_status, "true", StringComparison.OrdinalIgnoreCase);
            if (!isSuccess)
            {
                _logger.LogInformation("Received non-success Tpay notification for transaction {TransactionId}: {Status}", notification.tr_id, notification.tr_status);
                await response.WriteStringAsync($"TRUE - Received non-success Tpay notification for transaction {notification.tr_id}: {notification.tr_status}");
                return response;
            }

            if (!record.PaymentSessionGuid.HasValue)
            {
                _logger.LogWarning("Missing payment session guid for reservation {ReservationGuid} during webhook.", record.ReservationGuid);
                await response.WriteStringAsync($"TRUE - Missing payment session guid for reservation {record.ReservationGuid} during webhook.");
                return response;
            }

            var webhookDto = new TpayWebhookDto
            {
                ReservationGuid = record.ReservationGuid,
                PaymentSessionGuid = record.PaymentSessionGuid.Value,
                ProviderTransactionId = notification.tr_id,
                Status = "PAID",
                Signature = "validated"
            };

            try
            {
                await _workflowService.HandleTpayWebhookAsync(webhookDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle Tpay webhook for reservation {ReservationGuid}.", record.ReservationGuid);
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
