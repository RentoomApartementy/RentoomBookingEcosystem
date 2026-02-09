using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class db_entities_foreigng_keys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "upsell_vouchers");

            migrationBuilder.DropTable(
                name: "upsell_order_lines");

            migrationBuilder.DropTable(
                name: "upsell_order_records");
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
                    table.ForeignKey(
                        name: "FK_upsell_order_records_reservation_records_reservation_guid",
                        column: x => x.reservation_guid,
                        principalTable: "reservation_records",
                        principalColumn: "reservation_guid",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    is_free_unlimited_uses = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upsell_order_lines", x => x.upsell_order_line_guid);
                    table.ForeignKey(
                        name: "FK_upsell_order_lines_upsell_order_records_upsell_order_guid",
                        column: x => x.upsell_order_guid,
                        principalTable: "upsell_order_records",
                        principalColumn: "upsell_order_guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "upsell_vouchers",
                columns: table => new
                {
                    upsell_voucher_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    upsell_order_line_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_guid = table.Column<Guid>(type: "uuid", nullable: false),
                    qr_token = table.Column<string>(type: "varchar", nullable: false),
                    code_short = table.Column<string>(type: "varchar", nullable: false),
                    status = table.Column<string>(type: "varchar", nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false),
                    last_used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upsell_vouchers", x => x.upsell_voucher_guid);
                    table.ForeignKey(
                        name: "FK_upsell_vouchers_upsell_order_lines_upsell_order_line_guid",
                        column: x => x.upsell_order_line_guid,
                        principalTable: "upsell_order_lines",
                        principalColumn: "upsell_order_line_guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_upsell_order_lines_upsell_order_guid",
                table: "upsell_order_lines",
                column: "upsell_order_guid");

            migrationBuilder.CreateIndex(
                name: "IX_upsell_order_records_reservation_guid",
                table: "upsell_order_records",
                column: "reservation_guid");

            migrationBuilder.CreateIndex(
                name: "idx_upsell_vouchers_reservation_guid",
                table: "upsell_vouchers",
                column: "reservation_guid");

            migrationBuilder.CreateIndex(
                name: "idx_upsell_vouchers_status_validity",
                table: "upsell_vouchers",
                columns: new[] { "status", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "IX_upsell_vouchers_code_short",
                table: "upsell_vouchers",
                column: "code_short",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upsell_vouchers_qr_token",
                table: "upsell_vouchers",
                column: "qr_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upsell_vouchers_upsell_order_line_guid",
                table: "upsell_vouchers",
                column: "upsell_order_line_guid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "upsell_vouchers");

            migrationBuilder.DropTable(
                name: "upsell_order_lines");

            migrationBuilder.DropTable(
                name: "upsell_order_records");
        }
    }
}
