using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class DropReservationGuidFromSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reservation_guid",
                table: "livechat_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reservation_guid",
                table: "livechat_sessions",
                type: "uuid",
                nullable: true);
        }
    }
}
