using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveChatCrmBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<int>(
                name: "client_bitrix_id",
                table: "livechat_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deal_bitrix_id",
                table: "livechat_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ido_reservation_id",
                table: "livechat_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reservation_guid",
                table: "livechat_sessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "client_bitrix_id",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "deal_bitrix_id",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "ido_reservation_id",
                table: "livechat_sessions");

            migrationBuilder.DropColumn(
                name: "reservation_guid",
                table: "livechat_sessions");
        }
    }
}
