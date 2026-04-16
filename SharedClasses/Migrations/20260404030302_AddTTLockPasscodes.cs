using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddTTLockPasscodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ttlock_passcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_token = table.Column<string>(type: "text", nullable: false),
                    ttlock_id = table.Column<int>(type: "integer", nullable: false),
                    keyboard_pwd_id = table.Column<int>(type: "integer", nullable: false),
                    keyboard_pwd = table.Column<string>(type: "text", nullable: false),
                    passcode_name = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ttlock_passcodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ttlock_passcodes_generated_at",
                table: "ttlock_passcodes",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "idx_ttlock_passcodes_reservation_token",
                table: "ttlock_passcodes",
                column: "reservation_token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ttlock_passcodes");
        }
    }
}
