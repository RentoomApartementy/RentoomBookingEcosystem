using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RentoomBooking.SharedClasses.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentMediaAssetsAndSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apartment_media_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    apartment_id = table.Column<int>(type: "integer", nullable: false),
                    ido_object_media_id = table.Column<int>(type: "integer", nullable: true),
                    ido_source_url = table.Column<string>(type: "varchar", nullable: false),
                    storage_key = table.Column<string>(type: "varchar", nullable: false),
                    content_type = table.Column<string>(type: "varchar", nullable: true),
                    extension = table.Column<string>(type: "varchar", nullable: true),
                    picture_display_sequence = table.Column<int>(type: "integer", nullable: false),
                    source_etag = table.Column<string>(type: "varchar", nullable: true),
                    source_last_modified_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_content_length = table.Column<long>(type: "bigint", nullable: true),
                    checksum_sha256 = table.Column<string>(type: "varchar", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apartment_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "apartment_media_sync_runs",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "varchar", nullable: false),
                    apartments_processed = table.Column<int>(type: "integer", nullable: false),
                    media_items_seen = table.Column<int>(type: "integer", nullable: false),
                    downloaded_count = table.Column<int>(type: "integer", nullable: false),
                    replaced_count = table.Column<int>(type: "integer", nullable: false),
                    deleted_count = table.Column<int>(type: "integer", nullable: false),
                    sequence_updated_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    summary_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apartment_media_sync_runs", x => x.run_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_apartment_media_assets_apartment_id_ido_source_url",
                table: "apartment_media_assets",
                columns: new[] { "apartment_id", "ido_source_url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_apartment_media_assets_apartment_id_picture_display_sequence",
                table: "apartment_media_assets",
                columns: new[] { "apartment_id", "picture_display_sequence" });

            migrationBuilder.CreateIndex(
                name: "idx_apartment_media_sync_runs_started_at",
                table: "apartment_media_sync_runs",
                column: "started_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apartment_media_assets");

            migrationBuilder.DropTable(
                name: "apartment_media_sync_runs");
        }
    }
}
