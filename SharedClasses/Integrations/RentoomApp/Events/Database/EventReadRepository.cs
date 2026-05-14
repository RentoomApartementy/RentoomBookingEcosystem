using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database
{
    public interface IEventReadRepository
    {
        Task<IReadOnlyList<EventSearchResultDto>> GetEventsAsync(EventSearchQuery query, CancellationToken cancellationToken);
    }

    public class EventReadRepository : IEventReadRepository
    {
        private readonly IDbContextFactory<RappEventsDbContext> _dbContextFactory;

        public EventReadRepository(IDbContextFactory<RappEventsDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IReadOnlyList<EventSearchResultDto>> GetEventsAsync(EventSearchQuery query, CancellationToken cancellationToken)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var eventsQuery =
                from sourceEvent in dbContext.SourceEvents.AsNoTracking()
                join occurrence in dbContext.SourceEventOccurrences.AsNoTracking()
                    on sourceEvent.Id equals occurrence.SourceEventId
                from enrichment in dbContext.EventAiEnrichments
                    .AsNoTracking()
                    .Where(e => e.SourceEventId == sourceEvent.Id)
                    .Where(e => e.IsCurrent)
                    .Where(e => query.Language == null || e.Language == query.Language)
                    .OrderByDescending(e => e.GeneratedAtUtc)
                    .Take(1)
                    .DefaultIfEmpty()
                where sourceEvent.IsActive
                      && occurrence.StartAtLocal < query.TimeTo
                      && (occurrence.EndAtLocal ?? occurrence.StartAtLocal) >= query.TimeFrom
                      && (query.City == null || sourceEvent.City == query.City)
                select new EventSearchResultDto
                {
                    EventId = sourceEvent.Id,
                    OccurrenceId = occurrence.Id,
                    SourceName = sourceEvent.SourceName,
                    SourceEventKey = sourceEvent.SourceEventKey,
                    SourceEventUrl = sourceEvent.SourceEventUrl,
                    Title = sourceEvent.Title,
                    Category = sourceEvent.Category,
                    City = sourceEvent.City,
                    VenueName = sourceEvent.VenueName,
                    VenueAddress = sourceEvent.VenueAddress,
                    ImageUrl = sourceEvent.ImageUrl,
                    OrganizerName = sourceEvent.OrganizerName,
                    PriceInfoText = sourceEvent.PriceInfoText,
                    StartAtLocal = occurrence.StartAtLocal,
                    EndAtLocal = occurrence.EndAtLocal,
                    TimeZone = occurrence.TimeZone,
                    DateText = occurrence.DateText,
                    TimeText = occurrence.TimeText,
                    TicketUrl = occurrence.TicketUrl,
                    PriceText = occurrence.PriceText,
                    Language = enrichment.Language,
                    SummaryShort = enrichment.SummaryShort,
                    SummaryMedium = enrichment.SummaryMedium,
                    SearchSummary = enrichment.SearchSummary,
                    CategoryPrimary = enrichment.CategoryPrimary,
                    TagsJson = enrichment.TagsJson,
                    AudienceTagsJson = enrichment.AudienceTagsJson,
                    WhyGoJson = enrichment.WhyGoJson,
                    IsFamilyFriendly = enrichment.IsFamilyFriendly,
                    IsGoodForCouples = enrichment.IsGoodForCouples,
                    IsGoodForRainyWeather = enrichment.IsGoodForRainyWeather,
                    IndoorOutdoor = enrichment.IndoorOutdoor,
                    EnergyLevel = enrichment.EnergyLevel
                };

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var category = query.Category.Trim();
                eventsQuery = eventsQuery.Where(x => (x.CategoryPrimary ?? x.Category) == category);
            }

            return await eventsQuery
                .OrderBy(x => x.StartAtLocal)
                .Take(query.Limit)
                .ToListAsync(cancellationToken);
        }
    }

    public class EventSearchQuery
    {
        public DateTime TimeFrom { get; init; }
        public DateTime TimeTo { get; init; }
        public string? City { get; init; } = "Toruń";
        public string? Category { get; init; }
        public string? Language { get; init; }="pl";
        public int Limit { get; init; } = 20;
    }

    public class EventSearchResultDto
    {
        public Guid EventId { get; init; }
        public Guid OccurrenceId { get; init; }
        public string? SourceName { get; init; }
        public string? SourceEventKey { get; init; }
        public string? SourceEventUrl { get; init; }
        public string? Title { get; init; }
        public string? Category { get; init; }
        public string? City { get; init; }
        public string? VenueName { get; init; }
        public string? VenueAddress { get; init; }
        public string? ImageUrl { get; init; }
        public string? OrganizerName { get; init; }
        public string? PriceInfoText { get; init; }
        public DateTime StartAtLocal { get; init; }
        public DateTime? EndAtLocal { get; init; }
        public string? TimeZone { get; init; }
        public string? DateText { get; init; }
        public string? TimeText { get; init; }
        public string? TicketUrl { get; init; }
        public string? PriceText { get; init; }
        public string? Language { get; init; }
        public string? SummaryShort { get; init; }
        public string? SummaryMedium { get; init; }
        public string? SearchSummary { get; init; }
        public string? CategoryPrimary { get; init; }
        public string? TagsJson { get; init; }
        public string? AudienceTagsJson { get; init; }
        public string? WhyGoJson { get; init; }
        public bool? IsFamilyFriendly { get; init; }
        public bool? IsGoodForCouples { get; init; }
        public bool? IsGoodForRainyWeather { get; init; }
        public string? IndoorOutdoor { get; init; }
        public string? EnergyLevel { get; init; }
    }
}
