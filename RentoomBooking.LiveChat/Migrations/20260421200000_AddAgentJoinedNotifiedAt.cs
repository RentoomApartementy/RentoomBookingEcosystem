using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentJoinedNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "agent_joined_notified_at",
                table: "livechat_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_joined_notified_at",
                table: "livechat_sessions");
        }
    }
}
