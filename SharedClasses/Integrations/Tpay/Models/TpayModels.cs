using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RentoomBooking.SharedClasses.Integrations.Tpay.Models
{
    public class TpaySettings
    {
        public string ApiBaseUrl { get; set; } = string.Empty;

        // OAuth 2.0 credentials (Open API keys)
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        // Optional defaults (you can still override per request)
        public string? SuccessUrl { get; set; }
        public string? ErrorUrl { get; set; }
        public string? NotificationUrl { get; set; }

        public string DefaultCurrency { get; set; } = "PLN";

        // <summary>
        /// Expected prefix for the x5u certificate URL used in webhook JWS signatures (e.g. https://secure.tpay.com).
        /// </summary>
        public string? JwsCertPrefix { get; set; }

        /// <summary>
        /// Root CA PEM URL for validating Tpay signing certificate chain.
        /// </summary>
        public string? RootCaPemUrl { get; set; }

        /// <summary>
        /// Merchant "security code" used to verify transaction settlement notifications md5sum
        /// (Merchant Panel -> Notifications -> Security tab).
        /// Optional but strongly recommended if you validate md5sum.
        /// </summary>
        public string? MerchantSecurityCode { get; set; }

        public string? RentoomSiteBaseUrl { get; set; }

        public bool IsConfigured()
            => !string.IsNullOrWhiteSpace(ApiBaseUrl)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && !string.IsNullOrWhiteSpace(RentoomSiteBaseUrl);
    }

    public class TpayPayer
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }

    /// <summary>
    /// Your app-level request model. The client can map this to the API payload:
    /// amount, description, payer, callbacks.notification.url, callbacks.payerUrls.success/error, hiddenDescription.
    /// </summary>
    public class TpayTransactionRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TpayPayer Payer { get; set; } = new();
        public string lang { get; set; } = "pl";
        public string? SuccessUrl { get; set; }

        /// <summary>
        /// Tpay docs call this "error" (callbacks.payerUrls.error).
        /// </summary>
        public string? ErrorUrl { get; set; }

        /// <summary>
        /// Back-compat alias if you still set FailureUrl in calling code.
        /// Prefer ErrorUrl going forward.
        /// </summary>
        [JsonIgnore]
        public string? FailureUrl
        {
            get => ErrorUrl;
            set => ErrorUrl = value;
        }

        public string? NotificationUrl { get; set; }

        /// <summary>
        /// Tpay calls this hiddenDescription (used later as tr_crc in settlement notification).
        /// </summary>
        public string? HiddenDescription { get; set; }

        /// <summary>
        /// Back-compat alias; maps to HiddenDescription.
        /// </summary>
        [JsonIgnore]
        public string? CustomData
        {
            get => HiddenDescription;
            set => HiddenDescription = value;
        }
    }

    /// <summary>
    /// What your app returns to UI after creating a transaction.
    /// </summary>
    public class TpayTransactionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        /// <summary>
        /// Redirect payer to this URL (transactionPaymentUrl from Tpay).
        /// </summary>
        public string? RedirectUrl { get; set; }

        /// <summary>
        /// Transaction title used later in webhook flow (`tr_id`).
        /// </summary>
        public string? TransactionId { get; set; }

        public string? RawResponse { get; set; }
    }

    /// <summary>
    /// API response schema for POST /transactions (TransactionCreated).
    /// (We keep Errors as JToken because Tpay error payloads can vary.)
    /// </summary>
    public class TpayTransactionCreatedResponse
    {
        [JsonProperty("result")]
        public string? Result { get; set; }

        [JsonProperty("requestId")]
        public string? RequestId { get; set; }

        [JsonProperty("transactionId")]
        public string? TransactionId { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("posId")]
        public string? PosId { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("date")]
        public TpayTransactionDates? Date { get; set; }

        [JsonProperty("amount")]
        public decimal? Amount { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("hiddenDescription")]
        public string? HiddenDescription { get; set; }

        [JsonProperty("payer")]
        public TpayPayerDetails? Payer { get; set; }

        [JsonProperty("payments")]
        public TpayPayments? Payments { get; set; }

        [JsonProperty("transactionPaymentUrl")]
        public string? TransactionPaymentUrl { get; set; }

        // Error payloads (shape may differ depending on endpoint / validation)
        [JsonProperty("errors")]
        public JToken? Errors { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    public class TpayTransactionDates
    {
        [JsonProperty("creation")]
        public string? Creation { get; set; }

        [JsonProperty("realization")]
        public string? Realization { get; set; }
    }

    public class TpayPayments
    {
        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("method")]
        public JToken? Method { get; set; }

        [JsonProperty("amountPaid")]
        public decimal? AmountPaid { get; set; }

        [JsonProperty("date")]
        public TpayPaymentsDates? Date { get; set; }
    }

    public class TpayPaymentsDates
    {
        [JsonProperty("realization")]
        public string? Realization { get; set; }
    }

    public class TpayPayerDetails : TpayPayer
    {
        [JsonProperty("payerId")]
        public string? PayerId { get; set; }

        [JsonProperty("address")]
        public string? Address { get; set; }

        [JsonProperty("city")]
        public string? City { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }

        [JsonProperty("postalCode")]
        public string? PostalCode { get; set; }
    }

    /// <summary>
    /// POST /oauth/auth response.
    /// </summary>
    public class TpayOAuthTokenResponse
    {
        [JsonProperty("issued_at")]
        public long? IssuedAt { get; set; }

        [JsonProperty("scope")]
        public string? Scope { get; set; }

        [JsonProperty("token_type")]
        public string? TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("client_id")]
        public string? ClientId { get; set; }

        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }
    }

    /// <summary>
    /// Transaction settlement notification (application/x-www-form-urlencoded).
    /// Keep these as strings to validate md5sum against the raw values.
    /// </summary>
    public class TpayTransactionSettlementNotification
    {
        // merchant id (numeric)
        public string? id { get; set; }

        // transaction title
        public string? tr_id { get; set; }

        public string? tr_date { get; set; }

        // equals hiddenDescription you sent
        public string? tr_crc { get; set; }

        public string? tr_amount { get; set; }
        public string? tr_paid { get; set; }
        public string? tr_desc { get; set; }

        // "true" on success, "chargeback" on full manual refund
        public string? tr_status { get; set; }

        public string? tr_error { get; set; }
        public string? tr_email { get; set; }

        // md5(id + tr_id + tr_amount + tr_crc + securityCode)
        public string? md5sum { get; set; }

        // "1" test, "0" normal
        public string? test_mode { get; set; }

        // optional card/tokenization fields
        public string? card_token { get; set; }
        public string? token_expiry_date { get; set; }
        public string? card_tail { get; set; }
        public string? card_brand { get; set; }
    }


    public class TpayCreatePaymentRequest
    {
        public RentoomBooking.SharedClasses.Models.Payments.PaymentFlowType FlowType { get; set; } = RentoomBooking.SharedClasses.Models.Payments.PaymentFlowType.Reservation;
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? SuccessUrl { get; set; }
        public string? ErrorUrl { get; set; }
        public RentoomBooking.SharedClasses.Models.Upsell.UpsellOrderRequest? UpsellOrder { get; set; }
    }

    public class TpayCreatePaymentResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionPaymentUrl { get; set; } = string.Empty;
        public Guid PaymentSessionGuid { get; set; }
        public TpayTransactionCreatedResponse? TpayFullResponse { get; set; }
    }
}
