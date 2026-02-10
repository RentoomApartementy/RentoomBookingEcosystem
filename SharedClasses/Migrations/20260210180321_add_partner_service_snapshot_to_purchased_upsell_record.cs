using Microsoft.EntityFrameworkCore.Migrations;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class add_partner_service_snapshot_to_purchased_upsell_record : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<PartnerServiceSnapshot>(
                name: "partner_service_definition_snapshot",
                table: "upsell_order_lines",
                type: "jsonb",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "partner_service_definition_snapshot",
                table: "upsell_order_lines");
        }
    }
}
