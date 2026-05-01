using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_content",
                table: "livechat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detected_language",
                table: "livechat_messages",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_translated",
                table: "livechat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "guest_auto_translate_enabled",
                table: "livechat_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_content",
                table: "livechat_messages");

            migrationBuilder.DropColumn(
                name: "detected_language",
                table: "livechat_messages");

            migrationBuilder.DropColumn(
                name: "is_translated",
                table: "livechat_messages");

            migrationBuilder.DropColumn(
                name: "guest_auto_translate_enabled",
                table: "livechat_sessions");
        }
    }
}
