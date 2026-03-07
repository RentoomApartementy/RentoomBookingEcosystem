using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RentoomBooking.SharedClasses.Database;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    [DbContext(typeof(PostgresBookingDbContext))]
    [Migration("20260306190000_AddUpdatedAtToReservations")]
    public class AddUpdatedAtToReservations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.Sql("UPDATE \"Reservations\" SET \"updated_at\" = \"created_at\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "Reservations");
        }
    }
}