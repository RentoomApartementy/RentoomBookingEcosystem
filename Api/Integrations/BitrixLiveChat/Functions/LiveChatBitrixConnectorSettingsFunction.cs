using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.LiveChat;
using RentoomBooking.SharedClasses.Configuration;

namespace RentoomBooking.Api.Integrations.BitrixLiveChat.Functions;

public sealed class LiveChatBitrixConnectorSettingsFunction
{
    private readonly BitrixLiveChatService _liveChatService;
    private readonly string _connectorId;
    private readonly ILogger<LiveChatBitrixConnectorSettingsFunction> _logger;

    public LiveChatBitrixConnectorSettingsFunction(
        BitrixLiveChatService liveChatService,
        IOptions<BitrixLiveChatOptions> options,
        ILogger<LiveChatBitrixConnectorSettingsFunction> logger)
    {
        _liveChatService = liveChatService;
        _connectorId = options.Value.ConnectorId;
        _logger = logger;
    }

    [Function("LiveChatBitrixConnectorSettings")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "staywell/livechat/bitrix-connector-settings")]
        HttpRequestData req,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Bitrix connector settings request: Method={Method}, URL={Url}",
            req.Method, req.Url);

        var (memberId, lineId) = await ParsePlacementRequestAsync(req);

        _logger.LogInformation(
            "Bitrix connector settings parsed: MemberId={MemberId}, LineId={LineId}",
            memberId, lineId);

        if (string.IsNullOrWhiteSpace(memberId) || lineId is null or 0)
        {
            _logger.LogWarning(
                "Bitrix connector settings: missing member_id or LINE. MemberId={MemberId}, LineId={LineId}",
                memberId, lineId);
            return await CreateHtmlResponseAsync(req, HttpStatusCode.OK, BuildErrorHtml(
                "Missing required parameters (member_id or LINE). Please try again from the Open Line settings."));
        }

        var host = req.Headers.TryGetValues("X-Forwarded-Host", out var forwardedHosts)
            ? forwardedHosts.First()
            : req.Url.Authority;
        var scheme = req.Headers.TryGetValues("X-Forwarded-Proto", out var forwardedProtos)
            ? forwardedProtos.First()
            : req.Url.Scheme;
        if (host.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
            scheme = "https";
        var baseUrl = new Uri($"{scheme}://{host}");

        try
        {
            await _liveChatService.ActivateConnectorForLineAsync(memberId, lineId.Value, baseUrl, ct);

            _logger.LogInformation(
                "Bitrix connector activated via placement for member={MemberId}, line={LineId}",
                memberId, lineId);

            return await CreateHtmlResponseAsync(req, HttpStatusCode.OK, BuildSuccessHtml(_connectorId, lineId.Value, baseUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bitrix connector settings activation failed for member={MemberId}, line={LineId}",
                memberId, lineId);
            return await CreateHtmlResponseAsync(req, HttpStatusCode.OK,
                BuildErrorHtml($"Activation failed: {ex.Message}"));
        }
    }

    private static async Task<(string? MemberId, int? LineId)> ParsePlacementRequestAsync(HttpRequestData req)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        foreach (var key in query.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            var val = query[key];
            if (!string.IsNullOrWhiteSpace(val)) values[key] = val;
        }

        var body = await req.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            var formValues = HttpUtility.ParseQueryString(body);
            foreach (var key in formValues.AllKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var val = formValues[key];
                if (!string.IsNullOrWhiteSpace(val)) values[key] = val;
            }
        }

        var memberId = values.GetValueOrDefault("member_id")
                       ?? values.GetValueOrDefault("MEMBER_ID");

        int? lineId = null;
        var lineStr = values.GetValueOrDefault("PLACEMENT_OPTIONS")
                      ?? values.GetValueOrDefault("LINE");

        if (int.TryParse(lineStr, out var parsed))
        {
            lineId = parsed;
        }
        else if (!string.IsNullOrWhiteSpace(lineStr))
        {
            try
            {
                using var json = JsonDocument.Parse(lineStr);
                if (json.RootElement.TryGetProperty("LINE", out var lineProp))
                {
                    var lineVal = lineProp.ValueKind == JsonValueKind.Number
                        ? lineProp.GetInt32()
                        : int.TryParse(lineProp.GetString(), out var p) ? p : (int?)null;
                    lineId = lineVal;
                }
            }
            catch (JsonException)
            {
                var opts = HttpUtility.ParseQueryString(lineStr);
                var lineFromOpts = opts["LINE"] ?? opts["line"];
                if (int.TryParse(lineFromOpts, out parsed))
                    lineId = parsed;
            }
        }

        return (memberId, lineId);
    }

    private static string BuildSuccessHtml(string connectorId, int lineId, Uri baseUrl)
    {
        var channelUrl = baseUrl.ToString().TrimEnd('/');
        var channelId = $"{connectorId}_line{lineId}";
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Connector Settings</title>
                <script src="https://api.bitrix24.com/api/v1/"></script>
            </head>
            <body>
                <p id="status">Activating Rentoom LiveChat connector for line <strong>{{lineId}}</strong>…</p>
                <script>
                    var statusEl = document.getElementById("status");
                    function log(msg) { statusEl.innerHTML += "<br>" + msg; }
                    try {
                        BX24.init(function() {
                            log("BX24 initialized, calling connector.data.set...");
                            BX24.callMethod(
                                "imconnector.connector.data.set",
                                {
                                    CONNECTOR: "{{connectorId}}",
                                    LINE: {{lineId}},
                                    DATA: {
                                        ID: "{{channelId}}",
                                        URL: "{{channelUrl}}",
                                        URL_IM: "{{channelUrl}}",
                                        NAME: "Rentoom LiveChat"
                                    }
                                },
                                function(result) {
                                    if (result.error()) {
                                        log('<span style="color:red;">connector.data.set error: ' + JSON.stringify(result.error()) + '</span>');
                                        return;
                                    }
                                    log("connector.data.set OK, calling imconnector.activate...");
                                    BX24.callMethod(
                                        "imconnector.activate",
                                        {
                                            CONNECTOR: "{{connectorId}}",
                                            LINE: {{lineId}},
                                            ACTIVE: 1
                                        },
                                        function(activateResult) {
                                            if (activateResult.error()) {
                                                log('<span style="color:orange;">activate error: ' + JSON.stringify(activateResult.error()) + '</span>');
                                            } else {
                                                log("activate OK: " + JSON.stringify(activateResult.data()));
                                            }
                                            BX24.installFinish();
                                            log('<span style="color:green;">Done!</span>');
                                        }
                                    );
                                }
                            );
                        });
                    } catch(e) {
                        log('<span style="color:red;">JS error: ' + e.message + '</span>');
                    }
                </script>
            </body>
            </html>
            """;
    }

    private static string BuildErrorHtml(string message) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>Connector Settings — Error</title>
            <script src="https://api.bitrix24.com/api/v1/"></script>
        </head>
        <body>
            <p style="color:red;">{{message}}</p>
        </body>
        </html>
        """;

    private static async Task<HttpResponseData> CreateHtmlResponseAsync(HttpRequestData req, HttpStatusCode statusCode,
        string html)
    {
        var response = req.CreateResponse(statusCode);
        if (response.Headers.Contains("Content-Type")) response.Headers.Remove("Content-Type");
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await response.WriteStringAsync(html);
        return response;
    }
}
