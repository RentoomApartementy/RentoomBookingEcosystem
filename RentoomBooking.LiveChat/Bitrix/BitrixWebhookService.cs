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

    public async Task RegisterConnectorAsync(BitrixLiveChatPortalEntity portal, string connectorId, int openLineId,
        Uri webhookUrl, Uri placementHandlerUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
            throw new ArgumentException("connectorId is empty", nameof(connectorId));

        var connection = await _oauthService.GetPortalConnectionAsync(portal, ct);

        const string IconDataUri =
            "data:image/svg+xml;base64," +
            "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxMDAgMTAwIj48Y2lyY2xlIGN4PSI1MCIgY3k9IjUwIiByPSI0MCIgZmlsbD0iIzExQThFMCIvPjwvc3ZnPg==";

        using var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
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
            "Registering Bitrix imconnector for domain={Domain}, connectorId={ConnectorId}, openLineId={OpenLineId}",
            portal.Domain, connectorId, openLineId);

        var response = await _httpClient.PostAsync(requestUrl, requestContent, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation(
            "Bitrix imconnector.register response for domain={Domain} (HTTP {StatusCode}): {Body}",
            portal.Domain,
            (int)response.StatusCode,
            body);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Bitrix imconnector.register failed (HTTP {(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("error", out var errorProp))
        {
            var description = document.RootElement.TryGetProperty("error_description", out var descriptionProp)
                ? descriptionProp.ToString()
                : "Unknown Bitrix error.";
            throw new InvalidOperationException($"Bitrix imconnector.register failed: {errorProp} - {description}");
        }
    }
}