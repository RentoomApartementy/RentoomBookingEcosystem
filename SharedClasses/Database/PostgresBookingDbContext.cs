using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Cookies;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.StayWell;
using RentoomBooking.SharedClasses.Models.Upsell;

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
        public DbSet<ApartmentMediaAssetEntity> ApartmentMediaAssets => Set<ApartmentMediaAssetEntity>();
        public DbSet<ApartmentMediaSyncRunEntity> ApartmentMediaSyncRuns => Set<ApartmentMediaSyncRunEntity>();
        
        //MS: będziemy uzywac jednego z tych dwóch poniższych.... muszę pomyslec jak to zrefaktorowac odpowiednio.. 
        //MS: bo rezerwacje w ido NEW reservation vs Reservation to troche inne obiekty... 
        public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();
        public DbSet<ReservationRecordEntity> ReservationRecords => Set<ReservationRecordEntity>();
        public DbSet<BookingComLogEntity> BookingComLogs => Set<BookingComLogEntity>();
        public DbSet<ReservationTemplateEntity> ReservationTemplates => Set<ReservationTemplateEntity>();
        public DbSet<UpsellOrderRecordEntity> UpsellOrderRecords => Set<UpsellOrderRecordEntity>();
        public DbSet<UpsellOrderLineEntity> UpsellOrderLines => Set<UpsellOrderLineEntity>();
        
        
        public DbSet<SearchFiltersEntity> SearchFilters => Set<SearchFiltersEntity>();
        public DbSet<TermsEntity> Terms => Set<TermsEntity>();
        public DbSet<RegistrationCardEntity> RegistrationCard => Set<RegistrationCardEntity>();

        public DbSet<DefinedAddonEntity> DefinedAddons => Set<DefinedAddonEntity>();
        
        public DbSet<CustomerTermsAndConditionsSource> CustomerTermsSources => Set<CustomerTermsAndConditionsSource>();
        public DbSet<CustomerTermsSourceTranslation> CustomerTermsSourceTranslations => Set<CustomerTermsSourceTranslation>();
        public DbSet<CustomerAgreedTerms> CustomerAgreedTerms => Set<CustomerAgreedTerms>();
        public DbSet<CookieNoticeSource> CookieNoticeSources => Set<CookieNoticeSource>();
        public DbSet<CookieNoticeTranslation> CookieNoticeTranslations => Set<CookieNoticeTranslation>();
        public DbSet<CookieConsentAudit> CookieConsentAudits => Set<CookieConsentAudit>();

        public DbSet<UpsellVoucherEntity> UpsellVouchers => Set<UpsellVoucherEntity>();

        public DbSet<TTLockPasscodeEntity> TTLockPasscodes => Set<TTLockPasscodeEntity>();

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

            modelBuilder.Entity<ApartmentMediaAssetEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).UseIdentityByDefaultColumn();
                entity.Property(e => e.IdoSourceUrl).HasColumnType("varchar").IsRequired();
                entity.Property(e => e.StorageKey).HasColumnType("varchar").IsRequired();
                entity.Property(e => e.ContentType).HasColumnType("varchar");
                entity.Property(e => e.Extension).HasColumnType("varchar");
                entity.Property(e => e.SourceEtag).HasColumnType("varchar");
                entity.Property(e => e.ChecksumSha256).HasColumnType("varchar");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
                entity.HasIndex(e => new { e.ApartmentId, e.IdoSourceUrl }).IsUnique();
                entity.HasIndex(e => new { e.ApartmentId, e.PictureDisplaySequence });
            });

            modelBuilder.Entity<ApartmentMediaSyncRunEntity>(entity =>
            {
                entity.HasKey(e => e.RunId);
                entity.Property(e => e.Status).HasColumnType("varchar").IsRequired();
                entity.Property(e => e.SummaryJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.StartedAt).HasDefaultValueSql("NOW()");
                entity.HasIndex(e => e.StartedAt).HasDatabaseName("idx_apartment_media_sync_runs_started_at");
            });

            modelBuilder.Entity<ReservationEntity>(entity =>
            {

                entity.HasKey(e => e.ResToken);
                entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<ReservationRecordEntity>(entity =>
            {

                entity.HasKey(e => e.ReservationGuid);
                //entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
                //entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            });

            modelBuilder.Entity<BookingComLogEntity>(entity =>
            {
                entity.HasKey(e => e.BookingComLogGuid);
                entity.Property(e => e.IncomingEmailJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.StepsJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.Status).HasColumnType("varchar").IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
                entity.HasIndex(e => e.ReservationId).HasDatabaseName("idx_bookingcom_log_reservation_id");
                entity.HasIndex(e => e.ReservationGuid).HasDatabaseName("idx_bookingcom_log_reservation_guid");
                entity.HasIndex(e => e.Status).HasDatabaseName("idx_bookingcom_log_status");
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
                        v => Newtonsoft.Json.JsonConvert.DeserializeObject<UpsellTileDto>(v) ?? new UpsellTileDto()
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

            modelBuilder.Entity<CustomerTermsAndConditionsSource>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(100).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
            });

            modelBuilder.Entity<CustomerTermsSourceTranslation>(entity =>
            {
                entity.HasIndex(e => new { e.TermsSourceId, e.Culture }).IsUnique();
                entity.Property(e => e.Culture).HasMaxLength(20).IsRequired();
                entity.HasOne(e => e.TermsSource)
                    .WithMany(s => s.Translations)
                    .HasForeignKey(e => e.TermsSourceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CookieNoticeSource>(entity =>
            {
                entity.HasIndex(e => new { e.AppCode, e.Version }).IsUnique();
                entity.Property(e => e.AppCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<CookieNoticeTranslation>(entity =>
            {
                entity.HasIndex(e => new { e.CookieNoticeSourceId, e.Culture }).IsUnique();
                entity.Property(e => e.Culture).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.AcceptLabel).HasMaxLength(100).IsRequired();
                entity.Property(e => e.MoreLabel).HasMaxLength(100).IsRequired();
                entity.Property(e => e.LessLabel).HasMaxLength(100).IsRequired();
                entity.Property(e => e.CloseLabel).HasMaxLength(100).IsRequired();
                entity.HasOne(e => e.CookieNoticeSource)
                    .WithMany(s => s.Translations)
                    .HasForeignKey(e => e.CookieNoticeSourceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CookieConsentAudit>(entity =>
            {
                entity.HasIndex(e => new { e.AppCode, e.AcceptedAtUtc });
                entity.Property(e => e.AppCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.CookieNoticeVersion).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Decision).HasMaxLength(32).IsRequired();
                entity.Property(e => e.Culture).HasMaxLength(20);
                entity.Property(e => e.ContentHash).HasMaxLength(64);
                entity.Property(e => e.IpAddress).HasMaxLength(64);
                entity.Property(e => e.AzureClientIp).HasMaxLength(64);
                entity.Property(e => e.UserAgent).HasMaxLength(2048);
                entity.Property(e => e.RequestPath).HasMaxLength(1024);
                entity.Property(e => e.Referrer).HasMaxLength(2048);
                entity.HasOne(e => e.CookieNoticeSource)
                    .WithMany()
                    .HasForeignKey(e => e.CookieNoticeSourceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TTLockPasscodeEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GeneratedAt).HasDefaultValueSql("NOW()");
                entity.HasIndex(e => e.ReservationToken).HasDatabaseName("idx_ttlock_passcodes_reservation_token");
                entity.HasIndex(e => e.GeneratedAt).HasDatabaseName("idx_ttlock_passcodes_generated_at");
            });

            var staticDate = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc);
            var cookieNoticeValidFrom = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc);
            const int stayWellCookieNoticeId = 1001;
            const int webCookieNoticeId = 1002;

            static string NormalizeSeedText(string value) => value.ReplaceLineEndings("\n");

            var cookieDetailsPl = NormalizeSeedText("""
<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji. Szczegółowe informacje na temat wszystkich plików cookie odpowiadających poszczególnym kategoriom zgody znajdują się poniżej.</p>
<p>Pliki cookie sklasyfikowane jako "niezbędne" są przechowywane w przeglądarce użytkownika, ponieważ są niezbędne do włączenia podstawowych funkcji witryny.</p>
<p>Korzystamy również z plików cookie innych firm, które pomagają nam analizować sposób korzystania ze strony przez użytkowników, a także przechowywać preferencje użytkownika oraz dostarczać mu istotnych dla niego treści i reklam. Tego typu pliki cookie będą przechowywane w przeglądarce tylko za uprzednią zgodą użytkownika.</p>
<p>Wyłączenie niektórych plików cookie może wpłynąć na jakość przeglądania strony.</p>
<h3>Niezbędny</h3>
<p>Zawsze aktywne. Niezbędne pliki cookie mają kluczowe znaczenie dla podstawowych funkcji witryny i witryna nie będzie działać w zamierzony sposób bez nich. Te pliki cookie nie przechowują żadnych danych umożliwiających identyfikację osoby.</p>
<h3>Funkcjonalny</h3>
<p>Funkcjonalne pliki cookie pomagają wykonywać pewne funkcje, takie jak udostępnianie zawartości witryny na platformach mediów społecznościowych, zbieranie informacji zwrotnych i inne funkcje stron trzecich.</p>
<h3>Analityka</h3>
<p>Analityczne pliki cookie służą do zrozumienia, w jaki sposób użytkownicy wchodzą w interakcję z witryną. Te pliki cookie pomagają dostarczać informacje o metrykach liczby odwiedzających, współczynniku odrzuceń, źródle ruchu itp.</p>
<h3>Występ</h3>
<p>Wydajnościowe pliki cookie służą do zrozumienia i analizy kluczowych wskaźników wydajności witryny, co pomaga zapewnić lepsze wrażenia użytkownika dla odwiedzających.</p>
<h3>Reklama</h3>
<p>Reklamowe pliki cookie służą do dostarczania użytkownikom spersonalizowanych reklam w oparciu o strony, które odwiedzili wcześniej, oraz do analizowania skuteczności kampanii reklamowej.</p>
""");

            var cookieDetailsEn = NormalizeSeedText("""
<p>We use cookies to help users navigate efficiently and perform certain functions. Detailed information about all cookies corresponding to each consent category is available below.</p>
<p>Cookies classified as "necessary" are stored in the user's browser because they are required to enable the basic functions of the website.</p>
<p>We also use third-party cookies that help us analyze how users interact with the website, remember user preferences, and deliver relevant content and advertising. These cookies are stored in the browser only with the user's prior consent.</p>
<p>Disabling some cookies may affect the quality of your browsing experience.</p>
<h3>Necessary</h3>
<p>Always active. Necessary cookies are essential for the core features of the website and the site cannot function properly without them. These cookies do not store any personally identifiable data.</p>
<h3>Functional</h3>
<p>Functional cookies help perform certain features such as sharing website content on social media platforms, collecting feedback, and other third-party features.</p>
<h3>Analytics</h3>
<p>Analytics cookies help us understand how users interact with the website. These cookies provide information about metrics such as visitor numbers, bounce rate, and traffic source.</p>
<h3>Performance</h3>
<p>Performance cookies are used to understand and analyze the key performance indicators of the website, helping deliver a better user experience for visitors.</p>
<h3>Advertising</h3>
<p>Advertising cookies are used to deliver personalized ads based on pages previously visited by the user and to analyze the effectiveness of advertising campaigns.</p>
""");

            var cookieSummaryPl = NormalizeSeedText("""
<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji.</p>
<p>Większość narzędzi analitycznych i marketingowych uruchamiamy dopiero po akceptacji.</p>
""");

            var cookieSummaryEn = NormalizeSeedText("""
<p>We use cookies to help users navigate efficiently and perform certain functions.</p>
<p>Most analytics and marketing tools are enabled only after consent is accepted.</p>
""");

            //CustomerTermsAndConditionsSource
            modelBuilder.Entity<CustomerTermsAndConditionsSource>().HasData(
                new CustomerTermsAndConditionsSource
                {
                    Id = 1,
                    ValidFrom = staticDate,
                    Code = "data_processing",
                    Description = "Zgoda na przetwarzanie danych osobowych przez serwis Rentoom (Administratora danych) w celu realizacji usług",
                    Link = "",
                    IsActive = true,
                    SortOrder = 1,
                    IsRequired = true,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 2,
                    ValidFrom = staticDate,
                    Code = "main_terms",
                    Description = "Regulamin główny serwisu i akceptacja IdoBooking",
                    Link = "",
                    IsActive = true,
                    SortOrder = 2,
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 3,
                    ValidFrom = staticDate,
                    Code = "whatsapp_contact",
                    Description = "Zgoda na komunikację i przesyłanie ofert przez WhatsApp",
                    Link = "",
                    IsActive = true,
                    SortOrder = 3,
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 4,
                    ValidFrom = staticDate,
                    Code = "bitrix_processing",
                    Description = "Zgoda na przetwarzanie danych w systemie CRM Bitrix24",
                    Link = "",
                    IsActive = true,
                    SortOrder = 4,
                    IsRequired = false,
                },
                new CustomerTermsAndConditionsSource
                {
                    Id = 5,
                    ValidFrom = staticDate,
                    Code = "marketing_offers",
                    Description = "Zgoda marketingowa na przesyłanie ofert handlowych",
                    Link = "",
                    IsActive = true,
                    SortOrder = 5,
                    IsRequired = false,
                }
            );

            modelBuilder.Entity<CustomerTermsSourceTranslation>().HasData(
                new CustomerTermsSourceTranslation { Id = 1, TermsSourceId = 1, Culture = "pl", Description = "Zgoda na przetwarzanie danych osobowych przez serwis Rentoom (Administratora danych) w celu realizacji usług", Link = "" },
                new CustomerTermsSourceTranslation { Id = 2, TermsSourceId = 2, Culture = "pl", Description = "Regulamin główny serwisu i akceptacja IdoBooking", Link = "" },
                new CustomerTermsSourceTranslation { Id = 3, TermsSourceId = 3, Culture = "pl", Description = "Zgoda na komunikację i przesyłanie ofert przez WhatsApp", Link = "" },
                new CustomerTermsSourceTranslation { Id = 4, TermsSourceId = 4, Culture = "pl", Description = "Zgoda na przetwarzanie danych w systemie CRM Bitrix24", Link = "" },
                new CustomerTermsSourceTranslation { Id = 5, TermsSourceId = 5, Culture = "pl", Description = "Zgoda marketingowa na przesyłanie ofert handlowych", Link = "" },
                new CustomerTermsSourceTranslation { Id = 6, TermsSourceId = 1, Culture = "en", Description = "Consent to personal data processing by Rentoom (data controller) to provide services", Link = "" },
                new CustomerTermsSourceTranslation { Id = 7, TermsSourceId = 2, Culture = "en", Description = "Main platform terms and IdoBooking acceptance", Link = "" },
                new CustomerTermsSourceTranslation { Id = 8, TermsSourceId = 3, Culture = "en", Description = "Consent to communication and offer messages via WhatsApp", Link = "" },
                new CustomerTermsSourceTranslation { Id = 9, TermsSourceId = 4, Culture = "en", Description = "Consent to personal data processing in Bitrix24 CRM", Link = "" },
                new CustomerTermsSourceTranslation { Id = 10, TermsSourceId = 5, Culture = "en", Description = "Marketing consent for commercial offer messages", Link = "" }
            );

            modelBuilder.Entity<CookieNoticeSource>().HasData(
                new CookieNoticeSource
                {
                    Id = stayWellCookieNoticeId,
                    AppCode = CookieConsentAppCodes.StayWell,
                    Version = "2026-03-basic",
                    IsActive = true,
                    ValidFromUtc = cookieNoticeValidFrom
                },
                new CookieNoticeSource
                {
                    Id = webCookieNoticeId,
                    AppCode = CookieConsentAppCodes.RentoomBookingWeb,
                    Version = "2026-03-basic",
                    IsActive = true,
                    ValidFromUtc = cookieNoticeValidFrom
                }
            );

            modelBuilder.Entity<CookieNoticeTranslation>().HasData(
                new CookieNoticeTranslation
                {
                    Id = 2001,
                    CookieNoticeSourceId = stayWellCookieNoticeId,
                    Culture = "pl-PL",
                    Title = "O plikach cookies",
                    BannerSummaryHtml = cookieSummaryPl,
                    DetailsHtml = cookieDetailsPl,
                    AcceptLabel = "Akceptuję",
                    MoreLabel = "Więcej",
                    LessLabel = "Pokaż mniej",
                    CloseLabel = "Close"
                },
                new CookieNoticeTranslation
                {
                    Id = 2002,
                    CookieNoticeSourceId = stayWellCookieNoticeId,
                    Culture = "en-US",
                    Title = "About cookies",
                    BannerSummaryHtml = cookieSummaryEn,
                    DetailsHtml = cookieDetailsEn,
                    AcceptLabel = "Accept",
                    MoreLabel = "More",
                    LessLabel = "Show less",
                    CloseLabel = "Close"
                },
                new CookieNoticeTranslation
                {
                    Id = 2003,
                    CookieNoticeSourceId = webCookieNoticeId,
                    Culture = "pl-PL",
                    Title = "O plikach cookies",
                    BannerSummaryHtml = cookieSummaryPl,
                    DetailsHtml = cookieDetailsPl,
                    AcceptLabel = "Akceptuję",
                    MoreLabel = "Więcej",
                    LessLabel = "Pokaż mniej",
                    CloseLabel = "Close"
                },
                new CookieNoticeTranslation
                {
                    Id = 2004,
                    CookieNoticeSourceId = webCookieNoticeId,
                    Culture = "en-US",
                    Title = "About cookies",
                    BannerSummaryHtml = cookieSummaryEn,
                    DetailsHtml = cookieDetailsEn,
                    AcceptLabel = "Accept",
                    MoreLabel = "More",
                    LessLabel = "Show less",
                    CloseLabel = "Close"
                }
            );
        }
    }

}

