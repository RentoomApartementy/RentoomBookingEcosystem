using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class reservation_recordsTableAddBitrixColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "client_bitrix_id",
                table: "reservation_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deal_bitrix_id",
                table: "reservation_records",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_bitrix_id",
                table: "reservation_records");

            migrationBuilder.DropColumn(
                name: "deal_bitrix_id",
                table: "reservation_records");
        }
    }
}
