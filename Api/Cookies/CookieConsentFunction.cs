using System.Net;
using System.Net.Sockets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.Cookies;
using RentoomBooking.SharedClasses.Services.Cookies;

namespace RentoomBooking.Api.Cookies
{
    public class CookieConsentFunction
    {
        private readonly CookieConsentService _cookieConsentService;
        private readonly ILogger<CookieConsentFunction> _logger;

        public CookieConsentFunction(
            CookieConsentService cookieConsentService,
            ILogger<CookieConsentFunction> logger)
        {
            _cookieConsentService = cookieConsentService;
            _logger = logger;
        }

        [Function("GetCookieNotice")]
        public async Task<HttpResponseData> GetCookieNoticeAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/cookies/{appCode}/notice")] HttpRequestData req,
            string appCode,
            CancellationToken cancellationToken)
        {
            var response = req.CreateResponse();

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var culture = query.Get("culture")
                    ?? query.Get("lang")
                    ?? query.Get("language")
                    ?? query.Get("locale");

                var notice = await _cookieConsentService.GetActiveNoticeAsync(appCode, culture);
                if (notice is null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(notice), cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load cookie notice for app {AppCode}.", appCode);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.", cancellationToken);
                return response;
            }
        }

        [Function("SaveCookieConsent")]
        public async Task<HttpResponseData> SaveCookieConsentAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/cookies/{appCode}/consents")] HttpRequestData req,
            string appCode,
            CancellationToken cancellationToken)
        {
            var response = req.CreateResponse();

            try
            {
                var body = await req.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(body))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Request body cannot be empty.", cancellationToken);
                    return response;
                }

                SaveCookieConsentRequest? payload;
                try
                {
                    payload = JsonConvert.DeserializeObject<SaveCookieConsentRequest>(body);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Invalid cookie consent payload for app {AppCode}.", appCode);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid JSON payload.", cancellationToken);
                    return response;
                }

                if (payload is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid cookie consent payload.", cancellationToken);
                    return response;
                }

                var metadata = BuildRequestMetadata(req);
                var result = await _cookieConsentService.SaveConsentAsync(appCode, payload, metadata);
                if (result is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Cookie consent payload is inconsistent with the active notice.", cancellationToken);
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(result), cancellationToken);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save cookie consent for app {AppCode}.", appCode);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.", cancellationToken);
                return response;
            }
        }

        private static CookieConsentRequestMetadata BuildRequestMetadata(HttpRequestData req)
        {
            var azureClientIp = GetHeaderValue(req, "X-Azure-ClientIP");
            var forwardedFor = GetHeaderValue(req, "X-Forwarded-For");

            return new CookieConsentRequestMetadata
            {
                AzureClientIp = azureClientIp,
                ForwardedForRaw = forwardedFor,
                IpAddress = ResolveClientIp(azureClientIp, forwardedFor),
                UserAgent = GetHeaderValue(req, "User-Agent"),
                RequestPath = req.Url.PathAndQuery,
                Referrer = GetHeaderValue(req, "Referer")
            };
        }

        private static string? GetHeaderValue(HttpRequestData req, string headerName)
        {
            return req.Headers.TryGetValues(headerName, out var values)
                ? values.FirstOrDefault()
                : null;
        }

        private static string? ResolveClientIp(string? azureClientIp, string? forwardedFor)
        {
            if (IsValidIpAddress(azureClientIp))
            {
                return azureClientIp;
            }

            if (string.IsNullOrWhiteSpace(forwardedFor))
            {
                return null;
            }

            foreach (var candidate in forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (IsValidIpAddress(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsValidIpAddress(string? value)
        {
            return IPAddress.TryParse(value, out var address)
                && address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
        }
    }
}
