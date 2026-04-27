using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentsToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachments",
                table: "livechat_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attachments",
                table: "livechat_messages");
        }
    }
}
