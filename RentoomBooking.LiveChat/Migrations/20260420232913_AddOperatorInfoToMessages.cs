using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorInfoToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "operator_name",
                table: "livechat_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operator_avatar_url",
                table: "livechat_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "operator_name",
                table: "livechat_messages");

            migrationBuilder.DropColumn(
                name: "operator_avatar_url",
                table: "livechat_messages");
        }
    }
}
