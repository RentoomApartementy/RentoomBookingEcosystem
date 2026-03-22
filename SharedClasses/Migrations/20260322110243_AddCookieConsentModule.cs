using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddCookieConsentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cookie_notice_sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ValidFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cookie_notice_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cookie_consent_audits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CookieNoticeSourceId = table.Column<int>(type: "integer", nullable: false),
                    CookieNoticeVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CookieNoticeTranslationId = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientConsentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Culture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AzureClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ForwardedForRaw = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Referrer = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ReservationGuid = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cookie_consent_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cookie_consent_audits_cookie_notice_sources_CookieNoticeSou~",
                        column: x => x.CookieNoticeSourceId,
                        principalTable: "cookie_notice_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cookie_notice_translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CookieNoticeSourceId = table.Column<int>(type: "integer", nullable: false),
                    Culture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BannerSummaryHtml = table.Column<string>(type: "text", nullable: false),
                    DetailsHtml = table.Column<string>(type: "text", nullable: false),
                    AcceptLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MoreLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LessLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CloseLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cookie_notice_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cookie_notice_translations_cookie_notice_sources_CookieNoti~",
                        column: x => x.CookieNoticeSourceId,
                        principalTable: "cookie_notice_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "cookie_notice_sources",
                columns: new[] { "Id", "AppCode", "IsActive", "ValidFromUtc", "ValidToUtc", "Version" },
                values: new object[,]
                {
                    { 1001, "staywell", true, new DateTime(2026, 3, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "2026-03-basic" },
                    { 1002, "rentoombookingweb", true, new DateTime(2026, 3, 22, 0, 0, 0, 0, DateTimeKind.Utc), null, "2026-03-basic" }
                });

            migrationBuilder.InsertData(
                table: "cookie_notice_translations",
                columns: new[] { "Id", "AcceptLabel", "BannerSummaryHtml", "CloseLabel", "CookieNoticeSourceId", "Culture", "DetailsHtml", "LessLabel", "MoreLabel", "Title" },
                values: new object[,]
                {
                    { 2001, "Akceptuję", "<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji.</p>\n<p>Większość narzędzi analitycznych i marketingowych uruchamiamy dopiero po akceptacji.</p>", "Close", 1001, "pl-PL", "<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji. Szczegółowe informacje na temat wszystkich plików cookie odpowiadających poszczególnym kategoriom zgody znajdują się poniżej.</p>\n<p>Pliki cookie sklasyfikowane jako \"niezbędne\" są przechowywane w przeglądarce użytkownika, ponieważ są niezbędne do włączenia podstawowych funkcji witryny.</p>\n<p>Korzystamy również z plików cookie innych firm, które pomagają nam analizować sposób korzystania ze strony przez użytkowników, a także przechowywać preferencje użytkownika oraz dostarczać mu istotnych dla niego treści i reklam. Tego typu pliki cookie będą przechowywane w przeglądarce tylko za uprzednią zgodą użytkownika.</p>\n<p>Wyłączenie niektórych plików cookie może wpłynąć na jakość przeglądania strony.</p>\n<h3>Niezbędny</h3>\n<p>Zawsze aktywne. Niezbędne pliki cookie mają kluczowe znaczenie dla podstawowych funkcji witryny i witryna nie będzie działać w zamierzony sposób bez nich. Te pliki cookie nie przechowują żadnych danych umożliwiających identyfikację osoby.</p>\n<h3>Funkcjonalny</h3>\n<p>Funkcjonalne pliki cookie pomagają wykonywać pewne funkcje, takie jak udostępnianie zawartości witryny na platformach mediów społecznościowych, zbieranie informacji zwrotnych i inne funkcje stron trzecich.</p>\n<h3>Analityka</h3>\n<p>Analityczne pliki cookie służą do zrozumienia, w jaki sposób użytkownicy wchodzą w interakcję z witryną. Te pliki cookie pomagają dostarczać informacje o metrykach liczby odwiedzających, współczynniku odrzuceń, źródle ruchu itp.</p>\n<h3>Występ</h3>\n<p>Wydajnościowe pliki cookie służą do zrozumienia i analizy kluczowych wskaźników wydajności witryny, co pomaga zapewnić lepsze wrażenia użytkownika dla odwiedzających.</p>\n<h3>Reklama</h3>\n<p>Reklamowe pliki cookie służą do dostarczania użytkownikom spersonalizowanych reklam w oparciu o strony, które odwiedzili wcześniej, oraz do analizowania skuteczności kampanii reklamowej.</p>", "Pokaż mniej", "Więcej", "O plikach cookies" },
                    { 2002, "Accept", "<p>We use cookies to help users navigate efficiently and perform certain functions.</p>\n<p>Most analytics and marketing tools are enabled only after consent is accepted.</p>", "Close", 1001, "en-US", "<p>We use cookies to help users navigate efficiently and perform certain functions. Detailed information about all cookies corresponding to each consent category is available below.</p>\n<p>Cookies classified as \"necessary\" are stored in the user's browser because they are required to enable the basic functions of the website.</p>\n<p>We also use third-party cookies that help us analyze how users interact with the website, remember user preferences, and deliver relevant content and advertising. These cookies are stored in the browser only with the user's prior consent.</p>\n<p>Disabling some cookies may affect the quality of your browsing experience.</p>\n<h3>Necessary</h3>\n<p>Always active. Necessary cookies are essential for the core features of the website and the site cannot function properly without them. These cookies do not store any personally identifiable data.</p>\n<h3>Functional</h3>\n<p>Functional cookies help perform certain features such as sharing website content on social media platforms, collecting feedback, and other third-party features.</p>\n<h3>Analytics</h3>\n<p>Analytics cookies help us understand how users interact with the website. These cookies provide information about metrics such as visitor numbers, bounce rate, and traffic source.</p>\n<h3>Performance</h3>\n<p>Performance cookies are used to understand and analyze the key performance indicators of the website, helping deliver a better user experience for visitors.</p>\n<h3>Advertising</h3>\n<p>Advertising cookies are used to deliver personalized ads based on pages previously visited by the user and to analyze the effectiveness of advertising campaigns.</p>", "Show less", "More", "About cookies" },
                    { 2003, "Akceptuję", "<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji.</p>\n<p>Większość narzędzi analitycznych i marketingowych uruchamiamy dopiero po akceptacji.</p>", "Close", 1002, "pl-PL", "<p>Używamy plików cookie, aby pomóc użytkownikom w sprawnej nawigacji i wykonywaniu określonych funkcji. Szczegółowe informacje na temat wszystkich plików cookie odpowiadających poszczególnym kategoriom zgody znajdują się poniżej.</p>\n<p>Pliki cookie sklasyfikowane jako \"niezbędne\" są przechowywane w przeglądarce użytkownika, ponieważ są niezbędne do włączenia podstawowych funkcji witryny.</p>\n<p>Korzystamy również z plików cookie innych firm, które pomagają nam analizować sposób korzystania ze strony przez użytkowników, a także przechowywać preferencje użytkownika oraz dostarczać mu istotnych dla niego treści i reklam. Tego typu pliki cookie będą przechowywane w przeglądarce tylko za uprzednią zgodą użytkownika.</p>\n<p>Wyłączenie niektórych plików cookie może wpłynąć na jakość przeglądania strony.</p>\n<h3>Niezbędny</h3>\n<p>Zawsze aktywne. Niezbędne pliki cookie mają kluczowe znaczenie dla podstawowych funkcji witryny i witryna nie będzie działać w zamierzony sposób bez nich. Te pliki cookie nie przechowują żadnych danych umożliwiających identyfikację osoby.</p>\n<h3>Funkcjonalny</h3>\n<p>Funkcjonalne pliki cookie pomagają wykonywać pewne funkcje, takie jak udostępnianie zawartości witryny na platformach mediów społecznościowych, zbieranie informacji zwrotnych i inne funkcje stron trzecich.</p>\n<h3>Analityka</h3>\n<p>Analityczne pliki cookie służą do zrozumienia, w jaki sposób użytkownicy wchodzą w interakcję z witryną. Te pliki cookie pomagają dostarczać informacje o metrykach liczby odwiedzających, współczynniku odrzuceń, źródle ruchu itp.</p>\n<h3>Występ</h3>\n<p>Wydajnościowe pliki cookie służą do zrozumienia i analizy kluczowych wskaźników wydajności witryny, co pomaga zapewnić lepsze wrażenia użytkownika dla odwiedzających.</p>\n<h3>Reklama</h3>\n<p>Reklamowe pliki cookie służą do dostarczania użytkownikom spersonalizowanych reklam w oparciu o strony, które odwiedzili wcześniej, oraz do analizowania skuteczności kampanii reklamowej.</p>", "Pokaż mniej", "Więcej", "O plikach cookies" },
                    { 2004, "Accept", "<p>We use cookies to help users navigate efficiently and perform certain functions.</p>\n<p>Most analytics and marketing tools are enabled only after consent is accepted.</p>", "Close", 1002, "en-US", "<p>We use cookies to help users navigate efficiently and perform certain functions. Detailed information about all cookies corresponding to each consent category is available below.</p>\n<p>Cookies classified as \"necessary\" are stored in the user's browser because they are required to enable the basic functions of the website.</p>\n<p>We also use third-party cookies that help us analyze how users interact with the website, remember user preferences, and deliver relevant content and advertising. These cookies are stored in the browser only with the user's prior consent.</p>\n<p>Disabling some cookies may affect the quality of your browsing experience.</p>\n<h3>Necessary</h3>\n<p>Always active. Necessary cookies are essential for the core features of the website and the site cannot function properly without them. These cookies do not store any personally identifiable data.</p>\n<h3>Functional</h3>\n<p>Functional cookies help perform certain features such as sharing website content on social media platforms, collecting feedback, and other third-party features.</p>\n<h3>Analytics</h3>\n<p>Analytics cookies help us understand how users interact with the website. These cookies provide information about metrics such as visitor numbers, bounce rate, and traffic source.</p>\n<h3>Performance</h3>\n<p>Performance cookies are used to understand and analyze the key performance indicators of the website, helping deliver a better user experience for visitors.</p>\n<h3>Advertising</h3>\n<p>Advertising cookies are used to deliver personalized ads based on pages previously visited by the user and to analyze the effectiveness of advertising campaigns.</p>", "Show less", "More", "About cookies" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_cookie_consent_audits_AppCode_AcceptedAtUtc",
                table: "cookie_consent_audits",
                columns: new[] { "AppCode", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cookie_consent_audits_CookieNoticeSourceId",
                table: "cookie_consent_audits",
                column: "CookieNoticeSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_cookie_notice_sources_AppCode_Version",
                table: "cookie_notice_sources",
                columns: new[] { "AppCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cookie_notice_translations_CookieNoticeSourceId_Culture",
                table: "cookie_notice_translations",
                columns: new[] { "CookieNoticeSourceId", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cookie_consent_audits");

            migrationBuilder.DropTable(
                name: "cookie_notice_translations");

            migrationBuilder.DropTable(
                name: "cookie_notice_sources");
        }
    }
}
