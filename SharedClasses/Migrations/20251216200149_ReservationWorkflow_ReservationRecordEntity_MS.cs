using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class ReservationWorkflow_ReservationRecordEntity_MS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_records",
                columns: table => new
                {
                    reservation_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_json = table.Column<string>(type: "jsonb", nullable: false),
                    ido_reservation_id = table.Column<int>(type: "integer", nullable: true),
                    ido_status = table.Column<string>(type: "text", nullable: true),
                    payment_session_guid = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: true),
                    provider_transaction_id = table.Column<string>(type: "text", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_records", x => x.reservation_guid);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_records");
        }
    }
}
