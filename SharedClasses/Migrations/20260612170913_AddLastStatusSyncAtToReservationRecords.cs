using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddLastStatusSyncAtToReservationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_status_sync_at",
                table: "reservation_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "defined_amenities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    amenity_id = table.Column<int>(type: "integer", nullable: false),
                    amenity_type_name = table.Column<string>(type: "text", nullable: false),
                    amenity_name = table.Column<string>(type: "text", nullable: false),
                    lang = table.Column<string>(type: "text", nullable: false),
                    icon_source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defined_amenities", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "defined_amenities");

            migrationBuilder.DropColumn(
                name: "last_status_sync_at",
                table: "reservation_records");
        }
    }
}
