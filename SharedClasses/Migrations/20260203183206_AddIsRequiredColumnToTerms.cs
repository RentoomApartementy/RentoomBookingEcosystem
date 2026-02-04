using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRequiredColumnToTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "customer_terms_sources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "customer_terms_sources",
                columns: new[] { "Id", "Description", "IsRequired", "Link", "ValidFrom" },
                values: new object[,]
                {
                    { 1, "Zgoda na przetwarzanie danych osobowych przez serwis Rentoom (Administratora danych) w celu realizacji usług", true, "", new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(7080) },
                    { 2, "Regulamin główny serwisu i akceptacja IdoBooking", false, "", new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070) },
                    { 3, "Zgoda na komunikację i przesyłanie ofert przez WhatsApp", false, "", new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070) },
                    { 4, "Zgoda na przetwarzanie danych w systemie CRM Bitrix24", false, "", new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070) },
                    { 5, "Zgoda marketingowa na przesyłanie ofert handlowych", false, "", new DateTime(2026, 2, 3, 18, 32, 5, 503, DateTimeKind.Utc).AddTicks(9070) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "customer_terms_sources",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "customer_terms_sources");
        }
    }
}
