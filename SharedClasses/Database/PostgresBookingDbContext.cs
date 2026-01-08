using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.StayWell;

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
        
        //MS: będziemy uzywac jednego z tych dwóch poniższych.... muszę pomyslec jak to zrefaktorowac odpowiednio.. 
        //MS: bo rezerwacje w ido NEW reservation vs Reservation to troche inne obiekty... 
        public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();
        public DbSet<ReservationRecordEntity> ReservationRecords => Set<ReservationRecordEntity>();
        
        
        public DbSet<SearchFiltersEntity> SearchFilters => Set<SearchFiltersEntity>();
        public DbSet<TermsEntity> Terms => Set<TermsEntity>();
        public DbSet<RegistrationCardEntity> RegistrationCard => Set<RegistrationCardEntity>();

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

            modelBuilder.Entity<TermsEntity>(entity =>
            {
                entity.HasKey(e => e.ResToken);
                entity.Property(e => e.VersionAccepted).HasColumnName("version_accepted").IsRequired();
                entity.Property(e => e.TypeAccepted).HasColumnName("type_accepted").IsRequired();
                entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at").IsRequired();
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<RegistrationCardEntity>(entity =>
            {
                entity.HasKey(e => e.ResToken);
                entity.Property(e => e.ContactEmail).HasColumnName("contact_mail").IsRequired();
                //entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").IsRequired();
                //entity.Property(e => e.PhoneCountryCode).HasColumnName("phone_country_code").IsRequired();
                entity.Property(e => e.CheckInTime).HasColumnName("check_in_time").IsRequired()
                    .HasConversion(
                        v => v.ToUniversalTime(),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                    );
                entity.Property(e => e.GuestsData).HasColumnName("guests_data").HasColumnType("jsonb").IsRequired()
                    .HasConversion(
                        v => Newtonsoft.Json.JsonConvert.SerializeObject(v),
                        v => Newtonsoft.Json.JsonConvert.DeserializeObject<List<RegistrationCardGuestModel>>(v) ?? new List<RegistrationCardGuestModel>()
                    );
            });

            
           }
    }

}
