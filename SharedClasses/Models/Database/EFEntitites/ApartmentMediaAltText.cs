using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models.Database.EFEntitites
{
    public enum ApartmentMediaAltTextSource
    {
        Manual = 0,
        AiSuggested = 1,
        AiSuggestedEdited = 2
    }

    // Owned entirely by RentoomApp (unlike ApartmentMediaAssetEntity, which is synced externally).
    // One row per (MediaAssetId, Culture) - mirrors the CookieNoticeSource/CookieNoticeTranslation
    // 1:many pattern so translations can be added later without a schema change.
    [Table("apartment_media_alt_texts")]
    public class ApartmentMediaAltText
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MediaAssetId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Culture { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string AltText { get; set; } = string.Empty;

        [Required]
        public ApartmentMediaAltTextSource Source { get; set; } = ApartmentMediaAltTextSource.Manual;

        [Required]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(200)]
        public string? UpdatedBy { get; set; }

        [MaxLength(128), Column("source_content_hash")]
        public string? SourceContentHash { get; set; }

        [MaxLength(200), Column("ai_agent_name")]
        public string? AiAgentName { get; set; }

        [MaxLength(50), Column("ai_agent_version")]
        public string? AiAgentVersion { get; set; }

        [MaxLength(200), Column("ai_response_id")]
        public string? AiResponseId { get; set; }
    }
}
