using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndContentMaxLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "livechat_messages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "idx_livechat_messages_bitrix_message_id",
                table: "livechat_messages",
                column: "bitrix_message_id");

            migrationBuilder.CreateIndex(
                name: "idx_livechat_sessions_bitrix_chat_id",
                table: "livechat_sessions",
                column: "bitrix_chat_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_livechat_sessions_bitrix_chat_id",
                table: "livechat_sessions");

            migrationBuilder.DropIndex(
                name: "idx_livechat_messages_bitrix_message_id",
                table: "livechat_messages");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "livechat_messages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);
        }
    }
}
