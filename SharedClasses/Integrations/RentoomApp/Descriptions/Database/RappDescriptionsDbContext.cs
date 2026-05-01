using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Database
{
    public class RappDescriptionsDbContext : DbContext
    {
        public RappDescriptionsDbContext(DbContextOptions<RappDescriptionsDbContext> options) : base(options) { }

        public DbSet<ApartmentDescriptionSet> DescriptionSets { get; set; }
        public DbSet<ApartmentDescriptionVariant> Variants { get; set; }
        public DbSet<ApartmentDescriptionVariantChannel> VariantChannels { get; set; }
        public DbSet<ApartmentDescriptionFaq> Faqs { get; set; }
        public DbSet<ApartmentDescriptionHighlight> Highlights { get; set; }
        public DbSet<ApartmentDescriptionSeoPhrase> SeoPhrases { get; set; }
        public DbSet<ApartmentDescriptionCoverage> Coverages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Definiujemy schemat dla wszystkich tabel, jesli nie zostal ustawiony w atrybutach
            // Chociaz w atrybutach [Table(..., Schema = "rentoom")] juz to zrobilismy.
        }
    }
}
