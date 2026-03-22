using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingComLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookingcom_log",
                columns: table => new
                {
                    bookingcom_log_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<int>(type: "integer", nullable: true),
                    reservation_guid = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "varchar", nullable: false),
                    processing_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    incoming_email_json = table.Column<string>(type: "jsonb", nullable: false),
                    steps_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookingcom_log", x => x.bookingcom_log_guid);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bookingcom_log_reservation_guid",
                table: "bookingcom_log",
                column: "reservation_guid");

            migrationBuilder.CreateIndex(
                name: "idx_bookingcom_log_reservation_id",
                table: "bookingcom_log",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookingcom_log_status",
                table: "bookingcom_log",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookingcom_log");
        }
    }
}
