using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Database
{
    public class RappSocialMediaDbContext : DbContext
    {
        public RappSocialMediaDbContext(DbContextOptions<RappSocialMediaDbContext> options) : base(options) { }
        public DbSet<ApartmentItemSocialMedia> ApartmentItemSocialMedia { get; set; }
    }
}
