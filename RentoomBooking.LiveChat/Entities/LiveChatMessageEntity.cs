using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.Api.LiveChat.Entities;

[Table("livechat_messages")]
public sealed class LiveChatMessageEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("session_id")]
    public Guid SessionId { get; set; }

    /// <summary>
    /// "guest" or "operator"
    /// </summary>
    [Column("sender")]
    public string Sender { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("bitrix_message_id")]
    public string? BitrixMessageId { get; set; }

    [Column("operator_name")]
    public string? OperatorName { get; set; }

    // TODO: TEMP — remove after debugging user.id resolution
    [Column("operator_bitrix_user_id")]
    public string? OperatorBitrixUserId { get; set; }

    [Column("operator_avatar_url")]
    public string? OperatorAvatarUrl { get; set; }

    /// <summary>JSON-serialized array of <see cref="RentoomBooking.SharedClasses.LiveChat.LiveChatAttachmentDto"/>.</summary>
    [Column("attachments")]
    public string? Attachments { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LiveChatSessionEntity? Session { get; set; }
}

public static class LiveChatSenders
{
    public const string Guest = "guest";
    public const string Operator = "operator";
    public const string System = "system";
}
