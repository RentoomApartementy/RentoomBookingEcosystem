using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddBitrixLiveChatPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitrix_livechat_portals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    client_endpoint = table.Column<string>(type: "text", nullable: false),
                    server_endpoint = table.Column<string>(type: "text", nullable: true),
                    access_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    application_token = table.Column<string>(type: "text", nullable: true),
                    access_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    event_handler_id = table.Column<long>(type: "bigint", nullable: true),
                    event_handler_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bitrix_livechat_portals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bitrix_livechat_portals_domain",
                table: "bitrix_livechat_portals",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_bitrix_livechat_portals_member_id",
                table: "bitrix_livechat_portals",
                column: "member_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitrix_livechat_portals");
        }
    }
}
