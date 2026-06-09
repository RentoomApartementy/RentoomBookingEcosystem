using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentCardMediaVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "card_generated_count",
                table: "apartment_media_sync_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "card_replaced_count",
                table: "apartment_media_sync_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "card_content_type",
                table: "apartment_media_assets",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "card_height",
                table: "apartment_media_assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_storage_key",
                table: "apartment_media_assets",
                type: "varchar",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "card_width",
                table: "apartment_media_assets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "card_generated_count",
                table: "apartment_media_sync_runs");

            migrationBuilder.DropColumn(
                name: "card_replaced_count",
                table: "apartment_media_sync_runs");

            migrationBuilder.DropColumn(
                name: "card_content_type",
                table: "apartment_media_assets");

            migrationBuilder.DropColumn(
                name: "card_height",
                table: "apartment_media_assets");

            migrationBuilder.DropColumn(
                name: "card_storage_key",
                table: "apartment_media_assets");

            migrationBuilder.DropColumn(
                name: "card_width",
                table: "apartment_media_assets");
        }
    }
}
