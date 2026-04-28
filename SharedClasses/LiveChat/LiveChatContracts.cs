namespace RentoomBooking.SharedClasses.LiveChat;

public sealed record LiveChatSendRequest(string ReservationToken, string Message, string? GuestName = null, string? GuestEmail = null);
public sealed record LiveChatHistoryRequest(string ReservationToken);
public sealed record LiveChatStreamRequest(string ReservationToken, string? After = null);

public sealed record LiveChatAttachmentDto(
    string Name,
    string? MimeType,
    long? Size,
    string? UrlPreview,
    string? UrlDownload);

public sealed record LiveChatMessageDto(Guid Id, string Sender, string Content, DateTime CreatedAt, string? SenderName = null, string? OperatorAvatarUrl = null, IReadOnlyList<LiveChatAttachmentDto>? Attachments = null, string? OperatorBitrixUserId = null);
public sealed record LiveChatSessionDto(Guid SessionId, string Status, List<LiveChatMessageDto> Messages);
public sealed record LinkPreviewDto(string Url, string? Title, string? Description, string? ImageUrl, string? Host);

/// <summary>
/// Encapsulates an operator message received from the Bitrix24 webhook.
/// </summary>
public sealed record IncomingOperatorMessage(
    string? ConnectorChatId,
    string? BitrixChatId,
    string? Text,
    string? BitrixMessageId,
    string? AuthorId,
    IReadOnlyList<LiveChatAttachmentDto>? Attachments = null);
