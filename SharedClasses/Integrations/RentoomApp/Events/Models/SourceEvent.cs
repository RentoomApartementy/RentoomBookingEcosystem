using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Models
{
    public class SourceEvent
    {
        public Guid Id { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SourceEventKey { get; set; } = string.Empty;
        public string SourceEventUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DescriptionFull { get; set; }
        public string? Category { get; set; }
        public string? CategorySource { get; set; }
        public string? VenueName { get; set; }
        public string? VenueAddress { get; set; }
        public string? OrganizerName { get; set; }
        public string? PriceInfoText { get; set; }
        public string City { get; set; } = "Toruń";
        public string? ImageUrl { get; set; }
        public string? RawPayloadJson { get; set; }
        public string? ContentHash { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset FirstSeenAtUtc { get; set; }
        public DateTimeOffset LastSeenAtUtc { get; set; }
        public ICollection<SourceEventOccurrence> Occurrences { get; set; } = new List<SourceEventOccurrence>();
        public ICollection<EventAiEnrichment> AiEnrichments { get; set; } = new List<EventAiEnrichment>();
    }

    public class SourceEventOccurrence
    {
        public Guid Id { get; set; }
        public Guid SourceEventId { get; set; }
        public DateTime StartAtLocal { get; set; }
        public DateTime? EndAtLocal { get; set; }
        public string TimeZone { get; set; } = "Europe/Warsaw";
        public string? DateText { get; set; }
        public string? TimeText { get; set; }
        public string? TicketUrl { get; set; }
        public string? PriceText { get; set; }
        public string? AvailabilityText { get; set; }
        public DateTimeOffset ScrapedAtUtc { get; set; }
        public SourceEvent SourceEvent { get; set; } = null!;
    }
}
