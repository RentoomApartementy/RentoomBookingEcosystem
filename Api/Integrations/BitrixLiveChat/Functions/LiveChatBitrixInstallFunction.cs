using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.LiveChat;
using RentoomBooking.LiveChat.Bitrix;

namespace RentoomBooking.Api.Integrations.BitrixLiveChat.Functions;

public sealed class LiveChatBitrixInstallFunction
{
    private const string InstallationCompletedHtml =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>Bitrix install complete</title>
            <script src="https://api.bitrix24.com/api/v1/"></script>
        </head>
        <body>
            <script>
                function finishInstall() {
                    if (window.BX24 && typeof window.BX24.installFinish === "function") {
                        window.BX24.installFinish();
                    }
                }

                if (window.BX24 && typeof window.BX24.init === "function") {
                    window.BX24.init(finishInstall);
                } else {
                    finishInstall();
                }
            </script>
            Bitrix livechat installation complete.
        </body>
        </html>
        """;

    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILogger<LiveChatBitrixInstallFunction> _logger;

    public LiveChatBitrixInstallFunction(
        BitrixLiveChatService liveChatService,
        ILogger<LiveChatBitrixInstallFunction> logger)
    {
        _liveChatService = liveChatService;
        _logger = logger;
    }

    [Function("LiveChatBitrixInstall")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "staywell/livechat/bitrix-install")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await BitrixInstallRequestParser.ParseAsync(req, ct);
        if (payload is null)
            return await CreateTextResponseAsync(req, HttpStatusCode.BadRequest,
                "Missing required Bitrix installation parameters.");

        var webhookUrl = new Uri($"{req.Url.Scheme}://{req.Url.Authority}/api/staywell/livechat/bitrix-webhook");

        try
        {
            var portal = await _liveChatService.InstallPortalAsync(payload, webhookUrl, ct);
            _logger.LogInformation(
                "Bitrix livechat app installed for member={MemberId}, domain={Domain}, handler={Handler}",
                portal.MemberId,
                portal.Domain,
                portal.EventHandlerUrl);

            return await CreateHtmlResponseAsync(req, HttpStatusCode.OK, InstallationCompletedHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bitrix livechat installation failed");
            return await CreateTextResponseAsync(req, HttpStatusCode.InternalServerError,
                $"Bitrix installation failed: {ex.Message}");
        }
    }

    private static async Task<HttpResponseData> CreateTextResponseAsync(HttpRequestData req, HttpStatusCode statusCode,
        string message)
    {
        var response = req.CreateResponse(statusCode);
        SetContentType(response, "text/plain; charset=utf-8");
        await response.WriteStringAsync(message);
        return response;
    }

    private static async Task<HttpResponseData> CreateHtmlResponseAsync(HttpRequestData req, HttpStatusCode statusCode,
        string html)
    {
        var response = req.CreateResponse(statusCode);
        SetContentType(response, "text/html; charset=utf-8");
        await response.WriteStringAsync(html);
        return response;
    }

    private static void SetContentType(HttpResponseData response, string contentType)
    {
        if (response.Headers.Contains("Content-Type")) response.Headers.Remove("Content-Type");

        response.Headers.Add("Content-Type", contentType);
    }

    private static class BitrixInstallRequestParser
    {
        public static async Task<BitrixLiveChatPortalInstallation?> ParseAsync(HttpRequestData req,
            CancellationToken ct)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            AddQueryStringValues(req.Url.Query, values);

            var body = await req.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var trimmed = body.TrimStart();
                if (trimmed.StartsWith("{", StringComparison.Ordinal) ||
                    trimmed.StartsWith("[", StringComparison.Ordinal))
                    AddJsonValues(body, values);
                else
                    AddQueryStringValues(body, values);
            }

            var memberId = GetFirst(values, "member_id", "auth.member_id", "auth[member_id]", "memberId");
            var domain = GetFirst(values, "DOMAIN", "domain", "auth.domain", "auth[domain]");
            var accessToken = GetFirst(values, "AUTH_ID", "access_token", "auth.access_token", "auth[access_token]");

            if (string.IsNullOrWhiteSpace(memberId) ||
                string.IsNullOrWhiteSpace(domain) ||
                string.IsNullOrWhiteSpace(accessToken))
                return null;

            var refreshToken = GetFirst(values, "REFRESH_ID", "refresh_token", "auth.refresh_token",
                "auth[refresh_token]");
            var clientEndpoint = GetFirst(values, "client_endpoint", "CLIENT_ENDPOINT", "auth.client_endpoint",
                "auth[client_endpoint]");
            var serverEndpoint = GetFirst(values, "server_endpoint", "SERVER_ENDPOINT", "auth.server_endpoint",
                "auth[server_endpoint]");

            return new BitrixLiveChatPortalInstallation(
                memberId.Trim(),
                domain.Trim(),
                clientEndpoint?.Trim() ?? string.Empty,
                serverEndpoint?.Trim(),
                accessToken.Trim(),
                refreshToken?.Trim(),
                GetFirst(values, "scope", "auth.scope", "auth[scope]"),
                GetFirst(values, "status", "auth.status", "auth[status]"),
                GetFirst(values, "application_token", "auth.application_token", "auth[application_token]", "APP_SID"),
                ParseAccessTokenExpiry(values));
        }

        private static DateTime? ParseAccessTokenExpiry(IReadOnlyDictionary<string, string> values)
        {
            var now = DateTime.UtcNow;

            var unixTimestamp = GetFirst(values, "auth.expires", "auth[expires]");
            if (long.TryParse(unixTimestamp, out var epochSeconds) && epochSeconds > 1_000_000_000)
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime;

            var secondsToExpire = GetFirst(values, "AUTH_EXPIRES", "expires_in", "auth.expires_in", "auth[expires_in]");
            if (double.TryParse(secondsToExpire, out var seconds)) return now.AddSeconds(seconds);

            return null;
        }

        private static void AddQueryStringValues(string queryOrFormBody, IDictionary<string, string> target)
        {
            var parsed = HttpUtility.ParseQueryString(queryOrFormBody.TrimStart('?'));
            foreach (var key in parsed.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                var value = parsed[key];
                if (!string.IsNullOrWhiteSpace(value)) target[key] = value;
            }
        }

        private static void AddJsonValues(string json, IDictionary<string, string> target)
        {
            using var document = JsonDocument.Parse(json);
            FlattenJson(document.RootElement, string.Empty, target);
        }

        private static void FlattenJson(JsonElement element, string prefix, IDictionary<string, string> target)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var childPrefix = string.IsNullOrEmpty(prefix)
                            ? property.Name
                            : $"{prefix}.{property.Name}";
                        FlattenJson(property.Value, childPrefix, target);
                    }

                    break;
                case JsonValueKind.Array:
                    break;
                case JsonValueKind.String:
                    target[prefix] = element.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    target[prefix] = element.ToString();
                    break;
            }
        }

        private static string? GetFirst(IReadOnlyDictionary<string, string> values, params string[] keys)
        {
            foreach (var key in keys)
                if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;

            return null;
        }
    }
}