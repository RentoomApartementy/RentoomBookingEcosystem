using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <summary>
    /// Intentionally empty. The apartment_media_alt_texts table is created and owned by the RentoomApp
    /// repository; this migration exists only so PostgresBookingDbContextModelSnapshot stays in sync with
    /// the entity mapped here, which keeps the next migration generated in this repo from re-creating it.
    ///
    /// The schema this mapping expects (verify with \d apartment_media_alt_texts before deploying):
    ///   Id                  integer identity, primary key
    ///   MediaAssetId        integer not null, FK -> apartment_media_assets(id) on delete cascade
    ///   Culture             varchar(20) not null
    ///   AltText             varchar(200) not null
    ///   Source              integer not null
    ///   UpdatedAtUtc        timestamp with time zone not null
    ///   UpdatedBy           varchar(200) null
    ///   source_content_hash varchar(128) null
    ///   ai_agent_name       varchar(200) null
    ///   ai_agent_version    varchar(50) null
    ///   ai_response_id      varchar(200) null
    ///   unique index on (MediaAssetId, Culture)
    /// </summary>
    public partial class AddApartmentMediaAltTextsMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
