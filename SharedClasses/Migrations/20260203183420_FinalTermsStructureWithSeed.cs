using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class FinalTermsStructureWithSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 1,
                column: "ValidFrom",
                value: new DateTime(2025, 2, 3, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 2,
                column: "ValidFrom",
                value: new DateTime(2025, 2, 3, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 3,
                column: "ValidFrom",
                value: new DateTime(2025, 2, 3, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 4,
                column: "ValidFrom",
                value: new DateTime(2025, 2, 3, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 5,
                column: "ValidFrom",
                value: new DateTime(2025, 2, 3, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 1,
                column: "ValidFrom",
                value: new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(7080));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 2,
                column: "ValidFrom",
                value: new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 3,
                column: "ValidFrom",
                value: new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 4,
                column: "ValidFrom",
                value: new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070));

            migrationBuilder.UpdateData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 5,
                column: "ValidFrom",
                value: new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070));
        }
    }
}
