using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class add_Upsell_post_buy_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "upsell_order_lines",
                columns: table => new
                {
                    upsell_order_line_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    upsell_order_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_service_id = table.Column<int>(type: "integer", nullable: false),
                    title_snapshot = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    pricing_model = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price_gross = table.Column<decimal>(type: "numeric", nullable: false),
                    nights = table.Column<int>(type: "integer", nullable: false),
                    total_guests = table.Column<int>(type: "integer", nullable: false),
                    line_total_gross = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    line_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bitrix_product_id = table.Column<int>(type: "integer", nullable: true),
                    bitrix_line_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upsell_order_lines", x => x.upsell_order_line_guid);
                });

            migrationBuilder.CreateTable(
                name: "upsell_order_records",
                columns: table => new
                {
                    upsell_order_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    upsell_order_json = table.Column<string>(type: "jsonb", nullable: false),
                    reservation_guid = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_session_guid = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: true),
                    provider_transaction_id = table.Column<string>(type: "text", nullable: true),
                    paid_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upsell_order_records", x => x.upsell_order_guid);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "upsell_order_lines");

            migrationBuilder.DropTable(
                name: "upsell_order_records");
        }
    }
}
