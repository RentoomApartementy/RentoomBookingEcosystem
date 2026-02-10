using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Models;
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
        public DbSet<ReservationTemplateEntity> ReservationTemplates => Set<ReservationTemplateEntity>();
        public DbSet<UpsellOrderRecordEntity> UpsellOrderRecords => Set<UpsellOrderRecordEntity>();
        public DbSet<UpsellOrderLineEntity> UpsellOrderLines => Set<UpsellOrderLineEntity>();
        
        
        public DbSet<SearchFiltersEntity> SearchFilters => Set<SearchFiltersEntity>();
        public DbSet<TermsEntity> Terms => Set<TermsEntity>();
        public DbSet<RegistrationCardEntity> RegistrationCard => Set<RegistrationCardEntity>();

        public DbSet<DefinedAddonEntity> DefinedAddons => Set<DefinedAddonEntity>();
        
        public DbSet<CustomerTermsAndConditionsSource> CustomerTermsSources => Set<CustomerTermsAndConditionsSource>();
        public DbSet<CustomerAgreedTerms> CustomerAgreedTerms => Set<CustomerAgreedTerms>();

        public DbSet<UpsellVoucherEntity> UpsellVouchers => Set<UpsellVoucherEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApartmentInfoEntity>(entity =>
            {

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
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

            modelBuilder.Entity<ReservationRecordEntity>(entity =>
            {

                entity.HasKey(e => e.ReservationGuid);
                //entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                //entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            });



            modelBuilder.Entity<ReservationTemplateEntity>(entity =>
            {
                entity.HasKey(e => e.TemplateKey);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<UpsellOrderRecordEntity>(entity =>
            {
                entity.HasKey(e => e.UpsellOrderGuid);
                entity.Property(e => e.UpsellOrderJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
                entity.HasOne(ps => ps.ReservationRecord)
                    .WithMany(p => p.UpsellOrderRecords)
                    .HasForeignKey(ps => ps.ReservationGuid)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UpsellOrderLineEntity>(entity =>
            {
                entity.HasKey(e => e.UpsellOrderLineGuid);
                entity.Property(e => e.TitleSnapshot).HasMaxLength(512);
                entity.Property(e => e.LineStatus).HasMaxLength(32);
                entity.Property(e => e.BitrixLineId).HasMaxLength(128);
                entity.Property(e=> e.UpsellDefinitionSnapshot).HasColumnType("jsonb").IsRequired().HasConversion(
                        v => Newtonsoft.Json.JsonConvert.SerializeObject(v),
                        v => Newtonsoft.Json.JsonConvert.DeserializeObject<PartnerServiceSnapshot>(v) ?? new PartnerServiceSnapshot()
                    );
                entity.Property(e => e.IsFreeUnlimitedUses).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
                entity.HasOne(ps => ps.UpsellOrderRecord)
                    .WithMany(p => p.UpsellOrderLineEntities)
                    .HasForeignKey(ps => ps.UpsellOrderGuid)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UpsellVoucherEntity>(entity =>
            {
                entity.HasKey(e => e.UpsellVoucherGuid);
                entity.HasIndex(e => e.UpsellOrderLineGuid).IsUnique();
                entity.HasIndex(e => e.ReservationGuid).HasDatabaseName("idx_upsell_vouchers_reservation_guid");
                entity.HasIndex(e => new { e.Status, e.ValidFrom, e.ValidTo }).HasDatabaseName("idx_upsell_vouchers_status_validity");
                entity.HasIndex(e => e.QrToken).IsUnique();
                entity.HasIndex(e => e.CodeShort).IsUnique();
                entity.HasOne(ps=>ps.UpsellOrderLine)
                    .WithOne(p=>p.UpsellVoucher)
                    .HasForeignKey<UpsellVoucherEntity>(e => e.UpsellOrderLineGuid)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.QrToken).HasColumnType("varchar");
                entity.Property(e => e.CodeShort).HasColumnType("varchar");
                entity.Property(e => e.Status).HasColumnType("varchar");
                entity.Property(e => e.ValidFrom).HasColumnType("date");
                entity.Property(e => e.ValidTo).HasColumnType("date");
                entity.Property(e => e.UsedCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
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

            modelBuilder.Entity<DefinedAddonEntity>(entity =>
            {
                entity.HasKey(e => e.IdoBookingId);
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.PaymentType).HasColumnName("payment_type").HasConversion<string>().IsRequired();
                entity.Property(e => e.PriceGross).HasColumnName("price_gross");
                entity.Property(e => e.Vat).HasColumnName("vat");
                entity.Property(e => e.AddonDefinition).HasColumnName("addon_definition").HasColumnType("jsonb")
                    .HasConversion(
                        v => Newtonsoft.Json.JsonConvert.SerializeObject(v),
                        v => Newtonsoft.Json.JsonConvert.DeserializeObject<RentoomBooking.SharedClasses.Models.RentoomBooking.DefinedAddonDefinition>(v)
                            ?? new RentoomBooking.SharedClasses.Models.RentoomBooking.DefinedAddonDefinition()
                    );
            });
            
            var staticDate = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc);

            //CustomerTermsAndConditionsSource
            modelBuilder.Entity<CustomerTermsAndConditionsSource>().HasData(
                new CustomerTermsAndConditionsSource
                {
                    Id = 1,
                    ValidFrom = staticDate,
                    Description = "Zgoda na przetwarzanie danych osobowych przez serwis Rentoom (Administratora danych) w celu realizacji usług",
                    Link = "",
                    IsRequired = true,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 2,
                    ValidFrom = staticDate,
                    Description = "Regulamin główny serwisu i akceptacja IdoBooking",
                    Link = "",
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 3,
                    ValidFrom = staticDate,
                    Description = "Zgoda na komunikację i przesyłanie ofert przez WhatsApp",
                    Link = "",
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 4,
                    ValidFrom = staticDate,
                    Description = "Zgoda na przetwarzanie danych w systemie CRM Bitrix24",
                    Link = "",
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 5,
                    ValidFrom = staticDate,
                    Description = "Zgoda marketingowa na przesyłanie ofert handlowych",
                    Link = "",
                    IsRequired = false,
                }
            );
        }
    }

}
