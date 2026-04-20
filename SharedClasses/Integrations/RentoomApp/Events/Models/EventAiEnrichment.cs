using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Models
{
    [Table("EventAiEnrichments", Schema = "events")]
    public class EventAiEnrichment
    {
        [Key]
        public Guid Id { get; set; }
        public Guid SourceEventId { get; set; }
        public string Language { get; set; } = "pl";
        public string SourceContentHash { get; set; } = string.Empty;
        public string SummaryShort { get; set; } = string.Empty;
        public string SummaryMedium { get; set; } = string.Empty;
        public string SearchSummary { get; set; } = string.Empty;
        public string SearchDocument { get; set; } = string.Empty;
        public string CategoryPrimary { get; set; } = "inne";
        public string TagsJson { get; set; } = "[]";
        public string AudienceTagsJson { get; set; } = "[]";
        public string UtilityTagsJson { get; set; } = "[]";
        public string VibeTagsJson { get; set; } = "[]";
        public string WhyGoJson { get; set; } = "[]";
        public bool IsFamilyFriendly { get; set; }
        public bool IsGoodForCouples { get; set; }
        public bool IsGoodForRainyWeather { get; set; }
        public bool IsTouristFriendly { get; set; }
        public string IndoorOutdoor { get; set; } = "unknown";
        public string EnergyLevel { get; set; } = "unknown";
        public string ModelName { get; set; } = string.Empty;
        public string PromptVersion { get; set; } = string.Empty;
        public string RawAiResponseJson { get; set; } = "{}";
        public DateTimeOffset GeneratedAtUtc { get; set; }
        public bool IsCurrent { get; set; }
        public string GenerationStatus { get; set; }
        public string? ErrorMessage { get; set; }
        public SourceEvent SourceEvent { get; set; } = null!;
    }

    public static class EventAiEnrichmentGenerationStatuses
    {
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";
    }

}
