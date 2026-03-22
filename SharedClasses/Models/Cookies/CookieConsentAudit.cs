using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models.Cookies
{
    [Table("cookie_consent_audits")]
    public class CookieConsentAudit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string AppCode { get; set; } = string.Empty;

        [Required]
        public int CookieNoticeSourceId { get; set; }

        [ForeignKey(nameof(CookieNoticeSourceId))]
        public CookieNoticeSource CookieNoticeSource { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string CookieNoticeVersion { get; set; } = string.Empty;

        [Required]
        public int CookieNoticeTranslationId { get; set; }

        [Required]
        [MaxLength(32)]
        public string Decision { get; set; } = string.Empty;

        [Required]
        public Guid ClientConsentId { get; set; }

        [Required]
        public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Culture { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string ContentHash { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(64)]
        public string? AzureClientIp { get; set; }

        public string? ForwardedForRaw { get; set; }

        [MaxLength(2048)]
        public string? UserAgent { get; set; }

        [MaxLength(1024)]
        public string? RequestPath { get; set; }

        [MaxLength(2048)]
        public string? Referrer { get; set; }

        public Guid? ReservationGuid { get; set; }
    }
}
