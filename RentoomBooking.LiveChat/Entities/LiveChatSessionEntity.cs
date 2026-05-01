using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.LiveChat.Entities;

[Table("livechat_sessions")]
public sealed class LiveChatSessionEntity
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();

    [Column("reservation_token")] public string ReservationToken { get; set; } = string.Empty;

    [Column("bitrix_chat_id")] public string? BitrixChatId { get; set; }

    [Column("bitrix_session_id")] public string? BitrixSessionId { get; set; }

    [Column("guest_name")] public string? GuestName { get; set; }

    [Column("guest_email")] public string? GuestEmail { get; set; }

    [Column("ido_reservation_id")] public int? IdoReservationId { get; set; }

    [Column("client_bitrix_id")] public int? ClientBitrixId { get; set; }

    [Column("deal_bitrix_id")] public int? DealBitrixId { get; set; }

    [Column("status")] public string Status { get; set; } = LiveChatSessionStatuses.Active;

    [Column("guest_auto_translate_enabled")] public bool GuestAutoTranslateEnabled { get; set; } = true;

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LiveChatMessageEntity> Messages { get; set; } = new List<LiveChatMessageEntity>();
}

public static class LiveChatSessionStatuses
{
    public const string Active = "active";
    public const string Closed = "closed";
}