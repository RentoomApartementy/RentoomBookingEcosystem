using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.ChatAI.Entities;

[Table("chat_messages")]
public sealed class ChatMessageEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("conversation_id")]
    public Guid ConversationId { get; set; }

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("token_count")]
    public int TokenCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatConversationEntity? Conversation { get; set; }
}
