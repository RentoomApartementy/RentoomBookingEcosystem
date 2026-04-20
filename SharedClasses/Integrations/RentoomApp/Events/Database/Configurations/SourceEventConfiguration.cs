using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database.Configurations
{
    internal sealed class SourceEventConfiguration : IEntityTypeConfiguration<SourceEvent>
    {
        public void Configure(EntityTypeBuilder<SourceEvent> builder)
        {
            builder.ToTable("SourceEvents", "events");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.SourceName).IsRequired();
            builder.Property(x => x.SourceEventKey).IsRequired();
            builder.Property(x => x.SourceEventUrl).IsRequired();
            builder.Property(x => x.Title).IsRequired();
            builder.Property(x => x.Description);
            builder.Property(x => x.DescriptionFull);
            builder.Property(x => x.Category);
            builder.Property(x => x.CategorySource);
            builder.Property(x => x.VenueName);
            builder.Property(x => x.VenueAddress);
            builder.Property(x => x.OrganizerName);
            builder.Property(x => x.PriceInfoText);
            builder.Property(x => x.City).IsRequired();
            builder.Property(x => x.ImageUrl);
            builder.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
            builder.Property(x => x.ContentHash);
            builder.Property(x => x.IsActive).IsRequired();
            builder.Property(x => x.FirstSeenAtUtc).HasColumnType("timestamptz");
            builder.Property(x => x.LastSeenAtUtc).HasColumnType("timestamptz");

            builder.HasIndex(x => new { x.SourceName, x.SourceEventKey }).IsUnique();
            builder.HasIndex(x => x.IsActive);

            builder.HasMany(x => x.Occurrences)
                .WithOne(x => x.SourceEvent)
                .HasForeignKey(x => x.SourceEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.AiEnrichments)
                .WithOne(x => x.SourceEvent)
                .HasForeignKey(x => x.SourceEventId)
                .OnDelete(DeleteBehavior.Cascade);

           /* builder.HasMany(x => x.AiEnrichmentJobs)
                .WithOne(x => x.SourceEvent)
                .HasForeignKey(x => x.SourceEventId)
                .OnDelete(DeleteBehavior.Cascade);
           */
        }
    }

    internal sealed class SourceEventOccurrenceConfiguration : IEntityTypeConfiguration<SourceEventOccurrence>
    {
        public void Configure(EntityTypeBuilder<SourceEventOccurrence> builder)
        {
            builder.ToTable("SourceEventOccurrences", "events");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.StartAtLocal).HasColumnType("timestamp without time zone").IsRequired();
            builder.Property(x => x.EndAtLocal).HasColumnType("timestamp without time zone");
            builder.Property(x => x.TimeZone).IsRequired();
            builder.Property(x => x.DateText);
            builder.Property(x => x.TimeText);
            builder.Property(x => x.TicketUrl);
            builder.Property(x => x.PriceText);
            builder.Property(x => x.AvailabilityText);
            builder.Property(x => x.ScrapedAtUtc).HasColumnType("timestamptz");

            builder.HasIndex(x => new { x.SourceEventId, x.StartAtLocal, x.TimeText }).IsUnique();
        }
    }

    internal sealed class EventAiEnrichmentConfiguration : IEntityTypeConfiguration<EventAiEnrichment>
    {
        public void Configure(EntityTypeBuilder<EventAiEnrichment> builder)
        {
            builder.ToTable("EventAiEnrichments","events");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Language).IsRequired();
            builder.Property(x => x.SourceContentHash).IsRequired();
            builder.Property(x => x.SummaryShort).IsRequired();
            builder.Property(x => x.SummaryMedium).IsRequired();
            builder.Property(x => x.SearchSummary).IsRequired();
            builder.Property(x => x.SearchDocument).IsRequired();
            builder.Property(x => x.CategoryPrimary).IsRequired();
            builder.Property(x => x.TagsJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.AudienceTagsJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.UtilityTagsJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.VibeTagsJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.WhyGoJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.IndoorOutdoor).IsRequired();
            builder.Property(x => x.EnergyLevel).IsRequired();
            builder.Property(x => x.ModelName).IsRequired();
            builder.Property(x => x.PromptVersion).IsRequired();
            builder.Property(x => x.RawAiResponseJson).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.GeneratedAtUtc).HasColumnType("timestamptz");
            builder.Property(x => x.GenerationStatus).IsRequired();

            builder.HasIndex(x => x.SourceEventId);
            builder.HasIndex(x => x.IsCurrent);
            builder.HasIndex(x => new { x.SourceEventId, x.Language, x.SourceContentHash })
                .IsUnique()
                .HasFilter("\"IsCurrent\" = true");

            builder.HasOne(x => x.SourceEvent)
                .WithMany(x => x.AiEnrichments)
                .HasForeignKey(x => x.SourceEventId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
