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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preferred_language",
                table: "livechat_sessions");
        }
    }
}
