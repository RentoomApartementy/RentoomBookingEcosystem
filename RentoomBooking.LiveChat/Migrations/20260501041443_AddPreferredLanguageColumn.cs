using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredLanguageColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                table: "livechat_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                ALTER TABLE bitrix_livechat_portals ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT NOW();
                ALTER TABLE bitrix_livechat_portals ADD COLUMN IF NOT EXISTS event_handler_id bigint;
                ALTER TABLE bitrix_livechat_portals ADD COLUMN IF NOT EXISTS event_handler_url text;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "bitrix_livechat_portals");

            migrationBuilder.DropColumn(
                name: "event_handler_id",
                table: "bitrix_livechat_portals");

            migrationBuilder.DropColumn(
                name: "event_handler_url",
                table: "bitrix_livechat_portals");
        }
    }
}
