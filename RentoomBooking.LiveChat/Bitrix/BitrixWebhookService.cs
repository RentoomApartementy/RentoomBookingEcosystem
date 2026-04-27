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
            _logger.LogInformation(
                "Bitrix event handler already bound for domain={Domain}, reusing existing handler metadata.",
                portal.Domain);
            return portal.EventHandlerId;
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

    private static bool IsAlreadyBoundResponse(JsonElement root)
    {
        if (!root.TryGetProperty("error_description", out var descriptionProp)) return false;

        var description = descriptionProp.GetString();
        return !string.IsNullOrWhiteSpace(description) &&
               description.Contains("Handler already binded", StringComparison.OrdinalIgnoreCase);
    }
}