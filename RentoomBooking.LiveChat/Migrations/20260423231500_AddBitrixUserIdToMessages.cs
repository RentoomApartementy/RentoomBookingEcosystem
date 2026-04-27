using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.Api.LiveChat.Migrations
{
    /// <inheritdoc />
    public partial class AddBitrixUserIdToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "operator_bitrix_user_id",
                table: "livechat_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "operator_bitrix_user_id",
                table: "livechat_messages");
        }
    }
}
