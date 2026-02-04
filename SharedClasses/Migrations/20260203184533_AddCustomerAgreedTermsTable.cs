using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAgreedTermsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_agreed_terms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermsSourceId = table.Column<int>(type: "integer", nullable: false),
                    ReservationGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    AgreedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientBitrixId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_agreed_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_agreed_terms_customer_terms_sources_TermsSourceId",
                        column: x => x.TermsSourceId,
                        principalTable: "customer_terms_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_agreed_terms_TermsSourceId",
                table: "customer_agreed_terms",
                column: "TermsSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_agreed_terms");
        }
    }
}
