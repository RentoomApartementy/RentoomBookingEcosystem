using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Entities;

[Table("chat_conversations")]
public sealed class ChatConversationEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("reservation_token")]
    public string ReservationToken { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = ChatConversationStatuses.Active;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessageEntity> Messages { get; set; } = new List<ChatMessageEntity>();
}
