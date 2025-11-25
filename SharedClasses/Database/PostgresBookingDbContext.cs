using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;

namespace RentoomBooking.SharedClasses.Database
{
    public class PostgresBookingDbContext : DbContext
    {
        public PostgresBookingDbContext(DbContextOptions<PostgresBookingDbContext> options) : base(options)
        {
        }

        public DbSet<ApartmentInfoEntity> ApartmentInfos => Set<ApartmentInfoEntity>();
        public DbSet<ApartmentAmenityEntity> ApartmentAmenities => Set<ApartmentAmenityEntity>();
        public DbSet<ApartmentHashEntity> ApartmentHashes => Set<ApartmentHashEntity>();
        public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();
        public DbSet<SearchFiltersEntity> SearchFilters => Set<SearchFiltersEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApartmentInfoEntity>(entity =>
            {
              
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Payload). HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<ApartmentAmenityEntity>(entity =>
            {
              
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<ApartmentHashEntity>(entity =>
            {
               
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<ReservationEntity>(entity =>
            {
              
                entity.HasKey(e => e.ResToken);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<SearchFiltersEntity>(entity =>
            {
                entity.HasKey(e => e.FilterGroupName);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
            });

        }
    }

}
