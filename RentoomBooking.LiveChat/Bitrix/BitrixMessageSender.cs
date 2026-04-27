using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.Api.LiveChat.Entities;
using RentoomBooking.Api.LiveChat.Repositories;
using RentoomBooking.SharedClasses.Configuration;

namespace RentoomBooking.Api.LiveChat.Bitrix;

public sealed class BitrixMessageSender : IBitrixMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly IBitrixOAuthService _oauthService;
    private readonly ILiveChatSessionRepository _sessionRepo;
    private readonly string _connectorId;
    private readonly int _openLineId;
    private readonly ILogger<BitrixMessageSender> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public BitrixMessageSender(
        HttpClient httpClient,
        IBitrixOAuthService oauthService,
        ILiveChatSessionRepository sessionRepo,
        IOptions<BitrixLiveChatOptions> options,
        ILogger<BitrixMessageSender> logger)
    {
        _httpClient = httpClient;
        _oauthService = oauthService;
        _sessionRepo = sessionRepo;
        _logger = logger;

        var opt = options.Value;
        _connectorId = opt.ConnectorId;
        _openLineId = opt.OpenLineId;
    }

    public async Task<bool> SendGuestMessageToBitrixAsync(
        LiveChatSessionEntity session,
        LiveChatMessageEntity message,
        LiveChatCrmBindingTarget? crmTarget,
        CancellationToken ct)
    {
        try
        {
            var connection = await _oauthService.GetConnectionAsync(ct);
            var userCode = $"staywell_{session.Id}";
            var payload = BuildOutgoingBitrixMessagePayload(session, message, userCode, crmTarget);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Sending imconnector.send.messages CONNECTOR={Connector}, LINE={Line}, USER={User}",
                _connectorId, _openLineId, userCode);

            var response = await _httpClient.PostAsync(
                BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "imconnector.send.messages", connection.AccessToken),
                jsonContent, ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("imconnector.send.messages response (HTTP {StatusCode}): {Body}", (int)response.StatusCode, body);

            if (response.IsSuccessStatusCode)
            {
                await UpdateSessionBitrixIdentifiersAsync(session, body, ct);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Bitrix for session {SessionId}", session.Id);
            return false;
        }
    }

    public async Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct)
    {
        try
        {
            var connection = await _oauthService.GetConnectionAsync(ct);
            var payload = new
            {
                CONNECTOR = _connectorId,
                LINE = _openLineId,
                MESSAGES = new[]
                {
                    new
                    {
                        im = new { message_id = messageId },
                        chat = new { id = connectorChatId }
                    }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                BitrixRestHelpers.BuildRestMethodUrl(connection.ClientEndpoint, "imconnector.send.status.delivery", connection.AccessToken),
                jsonContent, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("imconnector.send.status.delivery response: {Body}", body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send delivery status for message {MessageId}", messageId);
        }
    }

    private async Task UpdateSessionBitrixIdentifiersAsync(LiveChatSessionEntity session, string responseBody, CancellationToken ct)
    {
        var identifiers = ExtractSessionIdentifiers(responseBody);
        var updated = false;

        if (!string.IsNullOrWhiteSpace(identifiers.ChatId) && !string.Equals(session.BitrixChatId, identifiers.ChatId, StringComparison.Ordinal))
        {
            session.BitrixChatId = identifiers.ChatId;
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(identifiers.SessionId) && !string.Equals(session.BitrixSessionId, identifiers.SessionId, StringComparison.Ordinal))
        {
            session.BitrixSessionId = identifiers.SessionId;
            updated = true;
        }

        if (!updated)
        {
            return;
        }

        session.UpdatedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session, ct);

        _logger.LogInformation(
            "Session {SessionId} linked to BitrixChatId={ChatId}, BitrixSessionId={BitrixSessionId}",
            session.Id,
            session.BitrixChatId,
            session.BitrixSessionId);
    }

    private static BitrixSessionIdentifiers ExtractSessionIdentifiers(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (TryFindSessionIdentifiers(document.RootElement, out var identifiers))
            {
                return identifiers;
            }
        }
        catch (JsonException)
        {
        }

        return BitrixSessionIdentifiers.Empty;
    }

    private static bool TryFindSessionIdentifiers(JsonElement element, out BitrixSessionIdentifiers identifiers)
    {
        if (TryExtractSessionIdentifiers(element, allowIdFallback: false, out identifiers))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (BitrixRestHelpers.TryGetPropertyIgnoreCase(element, "session", out var sessionElement) &&
                TryExtractSessionIdentifiers(sessionElement, allowIdFallback: true, out identifiers))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if ((property.NameEquals("session") || string.Equals(property.Name, "session", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    continue;
                }

                if (TryFindSessionIdentifiers(property.Value, out identifiers))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindSessionIdentifiers(item, out identifiers))
                {
                    return true;
                }
            }
        }

        identifiers = BitrixSessionIdentifiers.Empty;
        return false;
    }

    private static bool TryExtractSessionIdentifiers(
        JsonElement element,
        bool allowIdFallback,
        out BitrixSessionIdentifiers identifiers)
    {
        identifiers = BitrixSessionIdentifiers.Empty;

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var chatId = BitrixRestHelpers.GetJsonString(element, "CHAT_ID");
        var sessionId = BitrixRestHelpers.GetJsonString(element, "SESSION_ID");

        if (allowIdFallback && string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = BitrixRestHelpers.GetJsonString(element, "ID");
        }

        if (string.IsNullOrWhiteSpace(chatId) && string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        identifiers = new BitrixSessionIdentifiers(chatId, sessionId);
        return true;
    }

    private object BuildOutgoingBitrixMessagePayload(
        LiveChatSessionEntity session,
        LiveChatMessageEntity message,
        string userCode,
        LiveChatCrmBindingTarget? crmTarget)
    {
        var user = new Dictionary<string, object?>
        {
            ["id"] = userCode,
            ["name"] = session.GuestName ?? "Guest",
            ["last_name"] = string.Empty,
            ["avatar"] = string.Empty
        };

        if (!string.IsNullOrWhiteSpace(session.GuestEmail))
        {
            user["email"] = session.GuestEmail;
        }

        if (crmTarget is not null)
        {
            user["crm_entity"] = new Dictionary<string, object?>
            {
                ["type"] = crmTarget.EntityType,
                ["id"] = crmTarget.EntityId
            };
        }

        var chat = new Dictionary<string, object?>
        {
            ["id"] = userCode
        };

        if (crmTarget is not null)
        {
            chat["crm_entity"] = new Dictionary<string, object?>
            {
                ["type"] = crmTarget.EntityType,
                ["id"] = crmTarget.EntityId
            };
        }

        var outgoingMessage = new Dictionary<string, object?>
        {
            ["user"] = user,
            ["message"] = new Dictionary<string, object?>
            {
                ["id"] = message.Id.ToString(),
                ["text"] = message.Content
            },
            ["chat"] = chat
        };

        if (crmTarget is not null)
        {
            outgoingMessage["CRM_CREATE"] = "N";
            if (string.Equals(crmTarget.EntityType, "DEAL", StringComparison.OrdinalIgnoreCase))
            {
                outgoingMessage["CRM_DEAL_ID"] = crmTarget.EntityId;
            }
            else if (string.Equals(crmTarget.EntityType, "CONTACT", StringComparison.OrdinalIgnoreCase))
            {
                outgoingMessage["CRM_CONTACT_ID"] = crmTarget.EntityId;
            }
        }

        return new Dictionary<string, object?>
        {
            ["CONNECTOR"] = _connectorId,
            ["LINE"] = _openLineId,
            ["MESSAGES"] = new[] { outgoingMessage }
        };
    }

    private sealed record BitrixSessionIdentifiers(string? ChatId, string? SessionId)
    {
        public static readonly BitrixSessionIdentifiers Empty = new(null, null);
    }
}
