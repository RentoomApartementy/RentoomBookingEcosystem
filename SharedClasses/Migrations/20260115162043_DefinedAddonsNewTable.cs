using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class DefinedAddonsNewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "defined_addons",
                columns: table => new
                {
                    idobooking_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    payment_type = table.Column<string>(type: "text", nullable: false),
                    price_gross = table.Column<decimal>(type: "numeric", nullable: false),
                    vat = table.Column<decimal>(type: "numeric", nullable: false),
                    addon_definition = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defined_addons", x => x.idobooking_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "defined_addons");
        }
    }
}
