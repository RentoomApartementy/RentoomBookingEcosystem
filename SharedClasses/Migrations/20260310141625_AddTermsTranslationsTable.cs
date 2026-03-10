using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsTranslationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "customer_terms_sources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "customer_terms_sources",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "customer_terms_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "customer_terms_source_translations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermsSourceId = table.Column<int>(type: "integer", nullable: false),
                    Culture = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Link = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_terms_source_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_terms_source_translations_customer_terms_sources_T~",
                        column: x => x.TermsSourceId,
                        principalTable: "customer_terms_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "customer_terms_source_translations",
                columns: new[] { "Id", "Culture", "Description", "Link", "TermsSourceId" },
                values: new object[,]
                {
                    { 1, "pl", "Zgoda na przetwarzanie danych osobowych przez serwis Rentoom (Administratora danych) w celu realizacji usług", "", 1 },
                    { 2, "pl", "Regulamin główny serwisu i akceptacja IdoBooking", "", 2 },
                    { 3, "pl", "Zgoda na komunikację i przesyłanie ofert przez WhatsApp", "", 3 },
                    { 5, "pl", "Zgoda marketingowa na przesyłanie ofert handlowych", "", 5 },
                    { 6, "en", "Consent to personal data processing by Rentoom (data controller) to provide services", "", 1 },
                    { 7, "en", "Main platform terms and IdoBooking acceptance", "", 2 },
                    { 8, "en", "Consent to communication and offer messages via WhatsApp", "", 3 },
                    { 10, "en", "Marketing consent for commercial offer messages", "", 5 }
                });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "IsActive", "SortOrder" },
                values: new object[] { "data_processing", true, 1 });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "IsActive", "SortOrder" },
                values: new object[] { "main_terms", true, 2 });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Code", "IsActive", "SortOrder" },
                values: new object[] { "whatsapp_contact", true, 3 });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Code", "IsActive", "SortOrder" },
                values: new object[] { "bitrix_processing", true, 4 });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Code", "IsActive", "SortOrder" },
                values: new object[] { "marketing_offers", true, 5 });

            migrationBuilder.CreateIndex(
                name: "IX_customer_terms_sources_Code",
                table: "customer_terms_sources",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_terms_source_translations_TermsSourceId_Culture",
                table: "customer_terms_source_translations",
                columns: new[] { "TermsSourceId", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_terms_source_translations");

            migrationBuilder.DropIndex(
                name: "IX_customer_terms_sources_Code",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "customer_terms_sources");
        }
    }
}
