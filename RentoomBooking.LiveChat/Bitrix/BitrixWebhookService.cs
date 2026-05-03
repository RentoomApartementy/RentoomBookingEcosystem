using System.Text.Json;
using Microsoft.Extensions.Logging;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Bitrix;

public sealed class BitrixWebhookService : IBitrixWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BitrixWebhookService> _logger;
    private readonly IBitrixOAuthService _oauthService;

    public BitrixWebhookService(
        HttpClient httpClient,
        IBitrixOAuthService oauthService,
        ILogger<BitrixWebhookService> logger)
    {
        _httpClient = httpClient;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<long?> BindWebhookEventAsync(BitrixLiveChatPortalEntity portal, Uri webhookUrl,
        CancellationToken ct)
    {
        var connection = await _oauthService.GetPortalConnectionAsync(portal, ct);

        using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["event"] = "ONIMCONNECTORMESSAGEADD",
            ["handler"] = webhookUrl.ToString()
        });

        var response = await _httpClient.PostAsync(
            BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "event.bind", connection.AccessToken),
            requestContent,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation(
            "Bitrix event.bind response for domain={Domain} (HTTP {StatusCode}): {Body}",
            portal.Domain,
            (int)response.StatusCode,
            body);

        using var document = JsonDocument.Parse(body);

        if (IsAlreadyBoundResponse(document.RootElement))
        {
            var oldHandlerUrl = !string.IsNullOrWhiteSpace(portal.EventHandlerUrl)
                ? new Uri(portal.EventHandlerUrl)
                : webhookUrl;

            _logger.LogInformation(
                "Bitrix event handler already bound for domain={Domain}, unbinding old handler={OldHandler} and rebinding with new URL={NewHandler}.",
                portal.Domain, oldHandlerUrl, webhookUrl);

            await UnbindWebhookEventAsync(connection, portal, oldHandlerUrl, ct);
            return await BindAfterUnbindAsync(connection, portal, webhookUrl, ct);
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Bitrix event.bind failed (HTTP {(int)response.StatusCode}): {body}");

        if (document.RootElement.TryGetProperty("error", out var errorProp))
        {
            var description = document.RootElement.TryGetProperty("error_description", out var descriptionProp)
                ? descriptionProp.ToString()
                : "Unknown Bitrix error.";
            throw new InvalidOperationException($"Bitrix event.bind failed: {errorProp} - {description}");
        }

        if (!document.RootElement.TryGetProperty("result", out var resultProp)) return null;

        if (resultProp.ValueKind == JsonValueKind.Number && resultProp.TryGetInt64(out var numericResult))
            return numericResult;

        if (resultProp.ValueKind == JsonValueKind.String && long.TryParse(resultProp.GetString(), out numericResult))
            return numericResult;

        return null;
    }

    private async Task UnbindWebhookEventAsync(BitrixRestConnection connection, BitrixLiveChatPortalEntity portal,
        Uri webhookUrl, CancellationToken ct)
    {
        using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["event"] = "ONIMCONNECTORMESSAGEADD",
            ["handler"] = webhookUrl.ToString()
        });

        var response = await _httpClient.PostAsync(
            BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "event.unbind", connection.AccessToken),
            requestContent,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation(
            "Bitrix event.unbind response for domain={Domain} (HTTP {StatusCode}): {Body}",
            portal.Domain,
            (int)response.StatusCode,
            body);
    }

    private async Task<long?> BindAfterUnbindAsync(BitrixRestConnection connection, BitrixLiveChatPortalEntity portal,
        Uri webhookUrl, CancellationToken ct)
    {
        using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["event"] = "ONIMCONNECTORMESSAGEADD",
            ["handler"] = webhookUrl.ToString()
        });

        var response = await _httpClient.PostAsync(
            BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "event.bind", connection.AccessToken),
            requestContent,
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation(
            "Bitrix event.bind (after unbind) response for domain={Domain} (HTTP {StatusCode}): {Body}",
            portal.Domain,
            (int)response.StatusCode,
            body);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Bitrix event.bind (after unbind) failed (HTTP {(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("error", out var errorProp))
        {
            var description = document.RootElement.TryGetProperty("error_description", out var descriptionProp)
                ? descriptionProp.ToString()
                : "Unknown Bitrix error.";
            throw new InvalidOperationException($"Bitrix event.bind (after unbind) failed: {errorProp} - {description}");
        }

        if (!document.RootElement.TryGetProperty("result", out var resultProp)) return null;

        if (resultProp.ValueKind == JsonValueKind.Number && resultProp.TryGetInt64(out var numericResult))
            return numericResult;

        if (resultProp.ValueKind == JsonValueKind.String && long.TryParse(resultProp.GetString(), out numericResult))
            return numericResult;

        return null;
    }

    private static bool IsAlreadyBoundResponse(JsonElement root)
    {
        if (!root.TryGetProperty("error_description", out var descriptionProp)) return false;

        var description = descriptionProp.GetString();
        return !string.IsNullOrWhiteSpace(description) &&
               description.Contains("Handler already binded", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RegisterConnectorAsync(BitrixLiveChatPortalEntity portal, string connectorId,
        Uri placementHandlerUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
            throw new ArgumentException("connectorId is empty", nameof(connectorId));

        var connection = await _oauthService.GetPortalConnectionAsync(portal, ct);

        const string IconDataUri =
            "data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20viewBox%3D%220%200%20100%20100%22%3E%3Ccircle%20cx%3D%2250%22%20cy%3D%2250%22%20r%3D%2240%22%20fill%3D%22%2311A8E0%22%2F%3E%3C%2Fsvg%3E";

        using var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ID"] = connectorId,
            ["NAME"] = "Rentoom LiveChat",
            ["PLACEMENT_HANDLER"] = placementHandlerUrl.ToString(),
            ["ICON[DATA_IMAGE]"] = IconDataUri,
            ["ICON[COLOR]"] = "#11A8E0",
            ["ICON_DISABLED[DATA_IMAGE]"] = IconDataUri,
            ["ICON_DISABLED[COLOR]"] = "#11A8E0"
        });

        var requestUrl = BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "imconnector.register",
            connection.AccessToken);

        _logger.LogInformation(
            "Registering Bitrix imconnector for domain={Domain}, connectorId={ConnectorId}",
            portal.Domain, connectorId);

        var response = await _httpClient.PostAsync(requestUrl, registerContent, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation(
            "Bitrix imconnector.register response for domain={Domain} (HTTP {StatusCode}): {Body}",
            portal.Domain,
            (int)response.StatusCode,
            body);

        BitrixRestHelpers.EnsureBitrixSuccess(response, body, "imconnector.register");
    }

    public async Task SetConnectorDataAsync(BitrixLiveChatPortalEntity portal, string connectorId,
        int lineId, Uri channelBaseUrl, CancellationToken ct)
    {
        var connection = await _oauthService.GetPortalConnectionAsync(portal, ct);

        var channelId = $"{connectorId}_line{lineId}";
        var channelUrl = new Uri(channelBaseUrl, "/").ToString();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CONNECTOR"] = connectorId,
            ["LINE"] = lineId.ToString(),
            ["DATA[ID]"] = channelId,
            ["DATA[URL]"] = channelUrl,
            ["DATA[URL_IM]"] = channelUrl,
            ["DATA[NAME]"] = "Rentoom LiveChat"
        });

        var requestUrl = BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint,
            "imconnector.connector.data.set", connection.AccessToken);

        var response = await _httpClient.PostAsync(requestUrl, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation(
            "Bitrix imconnector.connector.data.set response for domain={Domain}, line={LineId} (HTTP {StatusCode}): {Body}",
            portal.Domain, lineId, (int)response.StatusCode, body);

        BitrixRestHelpers.EnsureBitrixSuccess(response, body, "imconnector.connector.data.set");
    }
}