using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCustomerTermsStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequiredForReservationWorkflow",
                table: "customer_terms_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleInStayWellOnboarding",
                table: "customer_terms_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TermsType",
                table: "customer_terms_sources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HtmlContent",
                table: "customer_terms_source_translations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "customer_terms_source_translations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_source_translations",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "HtmlContent", "Title" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsRequiredForReservationWorkflow", "IsVisibleInStayWellOnboarding", "TermsType" },
                values: new object[] { false, false, "Additional" });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsRequiredForReservationWorkflow", "IsVisibleInStayWellOnboarding", "TermsType" },
                values: new object[] { false, false, "Additional" });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsRequiredForReservationWorkflow", "IsVisibleInStayWellOnboarding", "TermsType" },
                values: new object[] { false, false, "Additional" });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsRequiredForReservationWorkflow", "IsVisibleInStayWellOnboarding", "TermsType" },
                values: new object[] { false, false, "Additional" });

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsRequiredForReservationWorkflow", "IsVisibleInStayWellOnboarding", "TermsType" },
                values: new object[] { false, false, "Additional" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRequiredForReservationWorkflow",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "IsVisibleInStayWellOnboarding",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "TermsType",
                table: "customer_terms_sources");

            migrationBuilder.DropColumn(
                name: "HtmlContent",
                table: "customer_terms_source_translations");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "customer_terms_source_translations");
        }
    }
}
