using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class upsell_vouchers_table_add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_free_unlimited_uses",
                table: "upsell_order_lines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.DropColumn(
                name: "is_free_unlimited_uses",
                table: "upsell_order_lines");
        }
    }
}
