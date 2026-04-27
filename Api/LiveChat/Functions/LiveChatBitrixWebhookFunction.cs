using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.LiveChat;
using RentoomBooking.SharedClasses.LiveChat;

namespace RentoomBooking.Api.LiveChat.Functions;

/// <summary>
/// Receives operator messages from Bitrix24 via webhook (event: OnImConnectorMessageAdd).
/// Register this URL in Bitrix24: POST https://&lt;api-domain&gt;/api/staywell/livechat/bitrix-webhook
/// </summary>
public sealed class LiveChatBitrixWebhookFunction
{
    private readonly BitrixLiveChatService _liveChatService;
    private readonly ILogger<LiveChatBitrixWebhookFunction> _logger;

    public LiveChatBitrixWebhookFunction(BitrixLiveChatService liveChatService, ILogger<LiveChatBitrixWebhookFunction> logger)
    {
        _liveChatService = liveChatService;
        _logger = logger;
    }

    [Function("LiveChatBitrixWebhook")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "staywell/livechat/bitrix-webhook")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadAsStringAsync();
        var contentType = req.Headers.TryGetValues("Content-Type", out var contentTypes)
            ? string.Join(", ", contentTypes)
            : null;

        _logger.LogInformation("Bitrix webhook received. Content-Type={ContentType}", contentType);
        _logger.LogDebug("Bitrix webhook raw body: {Body}", body);

        if (string.IsNullOrWhiteSpace(body))
        {
            return req.CreateResponse(HttpStatusCode.OK);
        }

        // Verify the application_token before processing any messages to prevent spoofed webhooks.
        var (authMemberId, authAppToken) = ExtractAuthFields(body);
        if (!await _liveChatService.VerifyWebhookApplicationTokenAsync(authMemberId, authAppToken, ct))
        {
            _logger.LogWarning(
                "Bitrix webhook: application_token verification failed for member_id={MemberId}",
                authMemberId);
            // Return 200 OK intentionally: Bitrix24 retries on non-2xx, returning 403 would cause a flood of retries.
            return req.CreateResponse(HttpStatusCode.OK);
        }

        try
        {
            var messages = ParseIncomingMessages(body, _logger);
            if (messages.Count == 0)
            {
                _logger.LogWarning("Bitrix webhook: no MESSAGES array in payload");
                return req.CreateResponse(HttpStatusCode.OK);
            }

            foreach (var msg in messages)
            {
                var hasText = !string.IsNullOrWhiteSpace(msg.Text);
                var hasAttachments = msg.Attachments.Count > 0;

                if (string.IsNullOrWhiteSpace(msg.ChatId) && string.IsNullOrWhiteSpace(msg.BitrixChatId))
                {
                    _logger.LogWarning("Bitrix webhook: skipping message with missing chat identifier");
                    continue;
                }

                if (!hasText && !hasAttachments)
                {
                    _logger.LogWarning("Bitrix webhook: skipping message with no text and no attachments");
                    continue;
                }

                _logger.LogInformation(
                    "Bitrix webhook: operator message for connectorChat={ConnectorChatId}, bitrixChat={BitrixChatId}, hasText={HasText}, attachmentCount={AttachmentCount}, authorId={AuthorId}",
                    msg.ChatId,
                    msg.BitrixChatId,
                    hasText,
                    msg.Attachments.Count,
                    msg.AuthorId ?? "(null)");

                var result = await _liveChatService.ReceiveOperatorMessageAsync(
                    new IncomingOperatorMessage(
                        msg.ChatId,
                        msg.BitrixChatId,
                        msg.Text,
                        msg.MessageId,
                        msg.AuthorId,
                        msg.Attachments),
                    ct);

                if (result is null) continue;

                // Confirm delivery only when message was successfully processed
                if (!string.IsNullOrWhiteSpace(msg.MessageId) && !string.IsNullOrWhiteSpace(msg.ChatId))
                {
                    await _liveChatService.SendDeliveryStatusAsync(msg.MessageId, msg.ChatId, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Bitrix webhook");
        }

        return req.CreateResponse(HttpStatusCode.OK);
    }

    private static List<IncomingBitrixMessage> ParseIncomingMessages(string body, ILogger logger)
    {
        if (TryParseJsonMessages(body, logger, out var jsonMessages) && jsonMessages.Count > 0)
        {
            return jsonMessages;
        }

        return ParseFormMessages(body);
    }

    /// <summary>
    /// Extracts <c>auth[member_id]</c> and <c>auth[application_token]</c> from the webhook body.
    /// Supports both JSON (<c>{ "auth": { "member_id": "...", "application_token": "..." } }</c>)
    /// and form-encoded (<c>auth[member_id]=...&amp;auth[application_token]=...</c>) payloads.
    /// Returns nulls for both if the fields are absent.
    /// </summary>
    private static (string? MemberId, string? ApplicationToken) ExtractAuthFields(string body)
    {
        var trimmed = body.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!TryGetPropertyIgnoreCase(root, "auth", out var authElement) ||
                    authElement.ValueKind != JsonValueKind.Object)
                {
                    return (null, null);
                }

                var memberId = GetStringIgnoreCase(authElement, "member_id");
                var appToken = GetStringIgnoreCase(authElement, "application_token");
                return (memberId, appToken);
            }
            catch (JsonException)
            {
                return (null, null);
            }
        }

        // Form-encoded: auth[member_id]=...&auth[application_token]=...
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var formMemberId = form["auth[member_id]"];
        var formAppToken = form["auth[application_token]"];
        return (formMemberId, formAppToken);
    }

    private static bool TryParseJsonMessages(string body, ILogger logger, out List<IncomingBitrixMessage> messages)
    {
        messages = [];

        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Documented payload format for OnImConnectorMessageAdd:
            // { "event": "ONIMCONNECTORMESSAGEADD", "data": { "CONNECTOR": "...", "LINE": ..., "MESSAGES": [...] }, "auth": {...} }
            var data = TryGetPropertyIgnoreCase(root, "data", out var dataElement) ? dataElement : root;
            if (!TryGetPropertyIgnoreCase(data, "MESSAGES", out var messagesElement) || messagesElement.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            foreach (var msg in messagesElement.EnumerateArray())
            {
                var attachments = ParseJsonFiles(msg, logger);
                var authorId = GetNestedStringIgnoreCase(msg, "message", "user_id")
                    ?? GetNestedStringIgnoreCase(msg, "message", "author_id");
                messages.Add(new IncomingBitrixMessage(
                    GetNestedStringIgnoreCase(msg, "chat", "id"),
                    GetNestedStringIgnoreCase(msg, "im", "chat_id"),
                    GetNestedStringIgnoreCase(msg, "im", "message_id"),
                    GetNestedStringIgnoreCase(msg, "message", "text"),
                    authorId,
                    attachments));
            }

            return true;
        }
        catch (JsonException)
        {
            messages = [];
            return false;
        }
    }

    /// <summary>
    /// Extracts file attachments from a Bitrix24 MESSAGES[N] element.
    /// Handles both array format and keyed-object format for the "files" property.
    /// </summary>
    private static IReadOnlyList<LiveChatAttachmentDto> ParseJsonFiles(JsonElement msgElement, ILogger logger)
    {
        if (!TryGetPropertyIgnoreCase(msgElement, "message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!TryGetPropertyIgnoreCase(messageElement, "files", out var filesElement))
        {
            return [];
        }

        var result = new List<LiveChatAttachmentDto>();

        IEnumerable<JsonElement> fileItems = filesElement.ValueKind switch
        {
            JsonValueKind.Array => filesElement.EnumerateArray(),
            JsonValueKind.Object => filesElement.EnumerateObject().Select(p => p.Value),
            _ => []
        };

        foreach (var file in fileItems)
        {
            if (file.ValueKind != JsonValueKind.Object) continue;

            // Log just the property names to help diagnose unexpected payload structures
            var fileKeys = file.EnumerateObject().Select(p => p.Name).ToArray();
            logger.LogTrace("Bitrix file object keys: [{Keys}]", string.Join(", ", fileKeys));

            var name = GetStringIgnoreCase(file, "name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Bitrix24 uses "type" (e.g. "image") rather than MIME type; accept both fields
            var mimeType = GetStringIgnoreCase(file, "mimeType")
                ?? GetStringIgnoreCase(file, "mime_type")
                ?? GetStringIgnoreCase(file, "type");

            _ = long.TryParse(GetStringIgnoreCase(file, "size") ?? "", out var size);

            var urlPreview = GetStringIgnoreCase(file, "urlPreview")
                ?? GetStringIgnoreCase(file, "url_preview");
            var urlDownload = GetStringIgnoreCase(file, "urlDownload")
                ?? GetStringIgnoreCase(file, "url_download")
                ?? GetStringIgnoreCase(file, "urlShow")
                ?? GetStringIgnoreCase(file, "url_show")
                ?? GetStringIgnoreCase(file, "link")
                ?? GetStringIgnoreCase(file, "url");

            result.Add(new LiveChatAttachmentDto(
                name,
                mimeType,
                size > 0 ? size : null,
                urlPreview,
                urlDownload));
        }

        return result;
    }

    private static string? GetStringIgnoreCase(JsonElement element, string prop)
    {
        if (TryGetPropertyIgnoreCase(element, prop, out var val))
        {
            return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
        }
        return null;
    }

    private static List<IncomingBitrixMessage> ParseFormMessages(string body)
    {
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var byIndex = new Dictionary<int, IncomingBitrixMessageBuilder>();
        // files[msgIndex][fileIndex][field]
        var filesByMsgIndex = new Dictionary<int, Dictionary<int, IncomingBitrixFileBuilder>>();

        foreach (var rawKey in form.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                continue;

            var value = form[rawKey];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            // Try standard message fields first
            if (TryParseFormMessageKey(rawKey, out var index, out var section, out var field))
            {
                if (!byIndex.TryGetValue(index, out var builder))
                {
                    builder = new IncomingBitrixMessageBuilder();
                    byIndex[index] = builder;
                }

                switch (section.ToLowerInvariant(), field.ToLowerInvariant())
                {
                    case ("chat", "id"):
                        builder.ChatId = value;
                        break;
                    case ("im", "chat_id"):
                        builder.BitrixChatId = value;
                        break;
                    case ("im", "message_id"):
                        builder.MessageId = value;
                        break;
                    case ("message", "text"):
                        builder.Text = value;
                        break;
                    case ("message", "user_id"):
                    case ("message", "author_id"):
                        builder.AuthorId ??= value;
                        break;
                }
                continue;
            }

            // Try file fields: data[MESSAGES][msgIdx][message][files][fileIdx][field]
            if (TryParseFormFileKey(rawKey, out var msgIdx, out var fileIdx, out var fileField))
            {
                if (!byIndex.ContainsKey(msgIdx))
                    byIndex[msgIdx] = new IncomingBitrixMessageBuilder();

                if (!filesByMsgIndex.TryGetValue(msgIdx, out var filesForMsg))
                {
                    filesForMsg = new Dictionary<int, IncomingBitrixFileBuilder>();
                    filesByMsgIndex[msgIdx] = filesForMsg;
                }

                if (!filesForMsg.TryGetValue(fileIdx, out var fileBuilder))
                {
                    fileBuilder = new IncomingBitrixFileBuilder();
                    filesForMsg[fileIdx] = fileBuilder;
                }

                switch (fileField.ToLowerInvariant())
                {
                    case "name": fileBuilder.Name = value; break;
                    case "mimetype":
                    case "mime_type":
                    case "type": fileBuilder.MimeType ??= value; break;
                    case "size": fileBuilder.Size = value; break;
                    case "urlpreview":
                    case "url_preview": fileBuilder.UrlPreview = value; break;
                    case "urldownload":
                    case "url_download":
                    case "urlshow":
                    case "url_show":
                    case "link":
                    case "url": fileBuilder.UrlDownload ??= value; break;
                }
            }
        }

        return byIndex
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var files = filesByMsgIndex.TryGetValue(x.Key, out var fmap)
                    ? fmap.OrderBy(f => f.Key).Select(f => f.Value.Build()).OfType<LiveChatAttachmentDto>().ToList()
                    : (IReadOnlyList<LiveChatAttachmentDto>)[];
                return x.Value.Build(files);
            })
            .Where(x => x is not null)
            .Cast<IncomingBitrixMessage>()
            .ToList();
    }

    private static bool TryParseFormMessageKey(string key, out int index, out string section, out string field)
    {
        index = -1;
        section = string.Empty;
        field = string.Empty;

        var segments = Regex.Matches(key, @"[^\[\]]+")
            .Select(match => match.Value)
            .ToArray();

        if (segments.Length < 4)
        {
            return false;
        }

        var offset = segments[0].Equals("data", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (segments.Length != offset + 4 ||
            !segments[offset].Equals("MESSAGES", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(segments[offset + 1], out index))
        {
            return false;
        }

        section = segments[offset + 2];
        field = segments[offset + 3];
        return true;
    }

    // Parses keys like: data[MESSAGES][0][message][files][0][name]
    private static bool TryParseFormFileKey(string key, out int msgIndex, out int fileIndex, out string fileField)
    {
        msgIndex = -1;
        fileIndex = -1;
        fileField = string.Empty;

        var segments = Regex.Matches(key, @"[^\[\]]+")
            .Select(m => m.Value)
            .ToArray();

        var offset = segments.Length > 0 && segments[0].Equals("data", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        // Expected: MESSAGES[msgIdx][message][files][fileIdx][field] → offset+6 segments total
        if (segments.Length != offset + 6)
            return false;

        if (!segments[offset].Equals("MESSAGES", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!int.TryParse(segments[offset + 1], out msgIndex))
            return false;

        if (!segments[offset + 2].Equals("message", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[offset + 3].Equals("files", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!int.TryParse(segments[offset + 4], out fileIndex))
            return false;

        fileField = segments[offset + 5];
        return true;
    }

    private static string? GetNestedStringIgnoreCase(JsonElement element, string parentProp, string childProp)
    {
        if (TryGetPropertyIgnoreCase(element, parentProp, out var parent) && parent.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(parent, childProp, out var child))
        {
            return child.ValueKind == JsonValueKind.String ? child.GetString() : child.ToString();
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed record IncomingBitrixMessage(string? ChatId, string? BitrixChatId, string? MessageId, string? Text, string? AuthorId, IReadOnlyList<LiveChatAttachmentDto> Attachments);

    private sealed class IncomingBitrixMessageBuilder
    {
        public string? ChatId { get; set; }
        public string? BitrixChatId { get; set; }
        public string? MessageId { get; set; }
        public string? Text { get; set; }
        public string? AuthorId { get; set; }

        public IncomingBitrixMessage? Build(IReadOnlyList<LiveChatAttachmentDto>? files = null)
        {
            var hasContent = !string.IsNullOrWhiteSpace(Text) || (files?.Count > 0);
            if (string.IsNullOrWhiteSpace(ChatId) &&
                string.IsNullOrWhiteSpace(BitrixChatId) &&
                string.IsNullOrWhiteSpace(MessageId) &&
                !hasContent)
            {
                return null;
            }

            return new IncomingBitrixMessage(ChatId, BitrixChatId, MessageId, Text, AuthorId, files ?? []);
        }
    }

    private sealed class IncomingBitrixFileBuilder
    {
        public string? Name { get; set; }
        public string? MimeType { get; set; }
        public string? Size { get; set; }
        public string? UrlPreview { get; set; }
        public string? UrlDownload { get; set; }

        public LiveChatAttachmentDto? Build()
        {
            if (string.IsNullOrWhiteSpace(Name)) return null;
            _ = long.TryParse(Size, out var size);
            return new LiveChatAttachmentDto(Name, MimeType, size > 0 ? size : null, UrlPreview, UrlDownload);
        }
    }
}
