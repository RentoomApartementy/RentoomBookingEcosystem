using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using System.Net.Http.Headers;
using System.Text;

namespace RentoomBooking.SharedClasses.Integrations.Tpay
{
    public interface ITpayClient
    {
        Task<TpayTransactionResult> CreateTransactionAsync(
            TpayTransactionRequest request,
            CancellationToken cancellationToken = default);
    }

    public class TpayClient : ITpayClient
    {
        private readonly HttpClient _httpClient;
        private readonly TpaySettings _settings;
        private readonly ILogger<TpayClient> _logger;

        // Token cache
        private string? _accessToken;
        private DateTimeOffset _accessTokenExpiresAtUtc;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public TpayClient(HttpClient httpClient, IOptions<TpaySettings> options, ILogger<TpayClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_settings.ApiBaseUrl))
                _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl);
        }

        public async Task<TpayTransactionResult> CreateTransactionAsync(
            TpayTransactionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var validationMessage = ValidateConfiguration();
            if (!string.IsNullOrEmpty(validationMessage))
            {
                return new TpayTransactionResult { Success = false, Message = validationMessage };
            }

            if (request.Amount <= 0)
            {
                return new TpayTransactionResult
                {
                    Success = false,
                    Message = "Payment amount must be greater than zero."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Payer?.Email))
            {
                return new TpayTransactionResult
                {
                    Success = false,
                    Message = "Payer email address is required for Tpay transactions."
                };
            }

            // 1) Ensure OAuth token
            var tokenResult = await EnsureAccessTokenAsync(cancellationToken);
            if (!tokenResult.Success)
            {
                return new TpayTransactionResult
                {
                    Success = false,
                    Message = tokenResult.Message ?? "Failed to obtain Tpay access token.",
                    RawResponse = tokenResult.RawResponse
                };
            }

            // 2) Create transaction
            var payload = BuildTransactionPayload(request);
            var serializedPayload = payload.ToString(Formatting.None);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("/transactions"));
            httpRequest.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var currencyForLog = string.IsNullOrWhiteSpace(request.Currency) ? _settings.DefaultCurrency : request.Currency;
            _logger.LogInformation("Creating Tpay transaction for {Amount} {Currency}", request.Amount, currencyForLog);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tpay transaction failed with status {Status}: {Body}", response.StatusCode, body);
                return new TpayTransactionResult
                {
                    Success = false,
                    Message = "Failed to create payment in Tpay (OpenAPI).",
                    RawResponse = body
                };
            }

            var parsed = JsonConvert.DeserializeObject<TpayTransactionCreatedResponse>(body);

            // If Tpay returned an error-ish payload
            var apiErrorMessage = ExtractApiErrorMessage(parsed);
            if (!string.IsNullOrWhiteSpace(apiErrorMessage))
            {
                return new TpayTransactionResult
                {
                    Success = false,
                    Message = apiErrorMessage,
                    TransactionId = parsed?.TransactionId ?? parsed?.Title,
                    RawResponse = body
                };
            }

            var redirectUrl = parsed?.TransactionPaymentUrl;
            var transactionId = parsed?.Title;//parsed?.TransactionId;

            if (string.IsNullOrWhiteSpace(redirectUrl))
            {
                _logger.LogWarning("Tpay did not return transactionPaymentUrl. TransactionId={TransactionId}. Raw: {Raw}",
                    transactionId, body);

                return new TpayTransactionResult
                {
                    Success = false,
                    Message = "Tpay did not return a redirect url for the transaction.",
                    TransactionId = transactionId,
                    RawResponse = body
                };
            }

            return new TpayTransactionResult
            {
                Success = true,
                RedirectUrl = redirectUrl,
                TransactionId = transactionId,
                RawResponse = body
            };
        }

        private string? ValidateConfiguration()
        {
            if (!_settings.IsConfigured())
                return "Tpay configuration is missing. Please provide OpenAPI ClientId/ClientSecret.";

            if (_httpClient.BaseAddress is null)
                return "Tpay ApiBaseUrl is missing.";

            return null;
        }

        private async Task<(bool Success, string? Message, string? RawResponse)> EnsureAccessTokenAsync(CancellationToken ct)
        {
            // Fast path: valid token cached
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                DateTimeOffset.UtcNow < _accessTokenExpiresAtUtc.AddSeconds(-30))
            {
                return (true, null, null);
            }

            await _tokenLock.WaitAsync(ct);
            try
            {
                // Check again after acquiring the lock
                if (!string.IsNullOrWhiteSpace(_accessToken) &&
                    DateTimeOffset.UtcNow < _accessTokenExpiresAtUtc.AddSeconds(-30))
                {
                    return (true, null, null);
                }

                using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("/oauth/auth"));
                tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _settings.ClientId,
                    ["client_secret"] = _settings.ClientSecret
                });

                using var resp = await _httpClient.SendAsync(tokenRequest, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tpay OAuth token failed with status {Status}: {Body}", resp.StatusCode, body);
                    return (false, "Failed to obtain OAuth token from Tpay.", body);
                }

                var parsed = JsonConvert.DeserializeObject<TpayOAuthTokenResponse>(body);
                if (string.IsNullOrWhiteSpace(parsed?.AccessToken) || parsed.ExpiresIn <= 0)
                {
                    _logger.LogWarning("Tpay OAuth token response missing fields. Raw: {Body}", body);
                    return (false, "Invalid OAuth token response from Tpay.", body);
                }

                _accessToken = parsed.AccessToken;
                _accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(parsed.ExpiresIn);

                return (true, null, body);
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private JObject BuildTransactionPayload(TpayTransactionRequest request)
        {
            // New model: SuccessUrl + ErrorUrl + NotificationUrl + HiddenDescription
            var successUrl = request.SuccessUrl ?? _settings.SuccessUrl;
            var errorUrl = request.ErrorUrl ?? _settings.ErrorUrl;
            var notificationUrl = request.NotificationUrl ?? _settings.NotificationUrl;

            if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(errorUrl))
                throw new InvalidOperationException("SuccessUrl and ErrorUrl are required (either in request or settings).");

            if (string.IsNullOrWhiteSpace(notificationUrl))
                throw new InvalidOperationException("NotificationUrl is required (either in request or settings).");

            var currency = string.IsNullOrWhiteSpace(request.Currency) ? _settings.DefaultCurrency : request.Currency;

            var root = new JObject
            {
                ["amount"] = request.Amount,
                ["currency"] = currency,
                ["description"] = request.Description,
                ["lang"] = ResolveTransactionLanguage(request.lang),
                ["payer"] = new JObject
                {
                    ["email"] = request.Payer!.Email,
                    ["name"] = request.Payer.Name
                },
                ["callbacks"] = new JObject
                {
                    ["notification"] = new JObject { ["url"] = notificationUrl },
                    ["payerUrls"] = new JObject
                    {
                        ["success"] = successUrl,
                        ["error"] = errorUrl
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(request.HiddenDescription))
                root["hiddenDescription"] = request.HiddenDescription;

            if (!string.IsNullOrWhiteSpace(request.Payer.Phone))
                ((JObject)root["payer"]!)["phone"] = request.Payer.Phone;

            return root;
        }

        private static string ResolveTransactionLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "pl";
            }

            return language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "pl";
        }

        private static string? ExtractApiErrorMessage(TpayTransactionCreatedResponse? parsed)
        {
            if (parsed is null) return "Invalid response from Tpay.";

            // Some APIs include "errors" or "message" on failures.
            if (parsed.Errors is not null)
            {
                // Best effort extraction (shape can vary)
                // Try common patterns: errors[0].message / errors.description / etc.
                var msg =
                    parsed.Errors.SelectToken("$..message")?.ToString() ??
                    parsed.Errors.SelectToken("$..description")?.ToString() ??
                    parsed.Errors.ToString(Formatting.None);

                if (!string.IsNullOrWhiteSpace(msg))
                    return msg;
            }

            // Sometimes a plain "message" is provided
            if (!string.IsNullOrWhiteSpace(parsed.Message))
                return parsed.Message;

            // If Result exists and is clearly non-success, treat it as error-ish
            if (!string.IsNullOrWhiteSpace(parsed.Result) &&
                !string.Equals(parsed.Result, "success", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(parsed.Result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return $"Tpay returned result='{parsed.Result}'.";
            }

            return null;
        }

        private Uri BuildUri(string relativePath)
        {
            if (Uri.TryCreate(relativePath, UriKind.Relative, out var rel))
                return new Uri(_httpClient.BaseAddress!, rel);

            return new Uri(relativePath, UriKind.RelativeOrAbsolute);
        }
    }
}
