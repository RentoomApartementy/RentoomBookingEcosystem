using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCrmBindingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bitrix_crm_bound_at",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "bitrix_crm_entity_id",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "bitrix_crm_entity_type",
                table: "livechat_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "bitrix_crm_bound_at",
                table: "livechat_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "bitrix_crm_entity_id",
                table: "livechat_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bitrix_crm_entity_type",
                table: "livechat_sessions",
                type: "text",
                nullable: true);
        }
    }
}
