using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Database
{
    public class RappNearbyAttractionsDbContext : DbContext
    {
        public RappNearbyAttractionsDbContext(DbContextOptions<RappNearbyAttractionsDbContext> options)
            : base(options) { }

        public DbSet<ApartmentNearbyAttractionsSet> ApartmentNearbyAttractionsSets { get; set; }
        public DbSet<ApartmentNearbyAttraction> ApartmentNearbyAttractions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApartmentNearbyAttractionsSet>()
                .HasMany(s => s.Attractions)
                .WithOne()
                .HasForeignKey(a => a.ApartmentItemId);
        }
    }
}
