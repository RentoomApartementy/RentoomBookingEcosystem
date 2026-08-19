using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Database;

public sealed class RappReviewsDbContext : DbContext
{
    public RappReviewsDbContext(DbContextOptions<RappReviewsDbContext> options)
        : base(options)
    {
    }

    internal DbSet<ApartmentReviewReadEntity> ApartmentReviews => Set<ApartmentReviewReadEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApartmentItemReviewReadEntity>(entity =>
        {
            entity.ToTable("ApartmentItems", "rentoom");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.ApartmentId).HasColumnName("ApartmentId");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.IsArchived).HasColumnName("isArchived");
        });

        modelBuilder.Entity<RentoomAppApartmentReadEntity>(entity =>
        {
            entity.ToTable("Apartments", "rentoom");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.HasMany(item => item.ApartmentItems)
                .WithOne(item => item.Apartment)
                .HasForeignKey(item => item.ApartmentId);
        });

        modelBuilder.Entity<ApartmentReviewReadEntity>(entity =>
        {
            entity.ToTable("apartment_reviews", "reviews");
            entity.HasKey(review => review.Id);
            entity.Property(review => review.ApartmentItemId).HasColumnName("apartment_item_id");
            entity.Property(review => review.Source).HasColumnName("source");
            entity.Property(review => review.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
            entity.Property(review => review.CheckinDate).HasColumnName("checkin_date");
            entity.Property(review => review.Score).HasColumnName("score");
            entity.Property(review => review.RatingScale).HasColumnName("rating_scale");
            entity.Property(review => review.PositiveText).HasColumnName("positive_text");
            entity.Property(review => review.OriginalLang).HasColumnName("original_lang");
            entity.Property(review => review.GuestName).HasColumnName("guest_name");
            entity.Property(review => review.GuestCountryName).HasColumnName("guest_country_name");
            entity.Property(review => review.GuestType).HasColumnName("guest_type");
            entity.Property(review => review.IsVisible).HasColumnName("is_visible");

            entity.HasOne(review => review.ApartmentItem)
                .WithMany()
                .HasForeignKey(review => review.ApartmentItemId);
        });
    }

}
