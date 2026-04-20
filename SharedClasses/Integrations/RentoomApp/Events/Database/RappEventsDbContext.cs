using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database.Configurations;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database
{
    public class RappEventsDbContext : DbContext
    {
        public RappEventsDbContext(DbContextOptions<RappEventsDbContext> options) : base(options)
        {
        }

        public DbSet<SourceEvent> SourceEvents => Set<SourceEvent>();
        public DbSet<SourceEventOccurrence> SourceEventOccurrences => Set<SourceEventOccurrence>();
        public DbSet<EventAiEnrichment> EventAiEnrichments => Set<EventAiEnrichment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new SourceEventConfiguration());
            modelBuilder.ApplyConfiguration(new SourceEventOccurrenceConfiguration());
            modelBuilder.ApplyConfiguration(new EventAiEnrichmentConfiguration());
        }
    }
}
