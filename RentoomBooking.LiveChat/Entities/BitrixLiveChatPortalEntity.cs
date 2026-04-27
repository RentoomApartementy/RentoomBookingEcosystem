using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.LiveChat.Entities;

[Table("bitrix_livechat_portals")]
public sealed class BitrixLiveChatPortalEntity
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();

    [Column("member_id")] public string MemberId { get; set; } = string.Empty;

    [Column("domain")] public string Domain { get; set; } = string.Empty;

    [Column("client_endpoint")] public string ClientEndpoint { get; set; } = string.Empty;

    [Column("server_endpoint")] public string? ServerEndpoint { get; set; }

    [Column("access_token")] public string? AccessToken { get; set; }

    [Column("refresh_token")] public string? RefreshToken { get; set; }

    [Column("scope")] public string? Scope { get; set; }

    [Column("status")] public string? Status { get; set; }

    [Column("application_token")] public string? ApplicationToken { get; set; }

    [Column("access_token_expires_at")] public DateTime? AccessTokenExpiresAt { get; set; }

    [Column("event_handler_id")] public long? EventHandlerId { get; set; }

    [Column("event_handler_url")] public string? EventHandlerUrl { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}