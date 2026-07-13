using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class SpatialLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable PostGIS (required for the geometry column + ST_Distance).
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            // Add the new spatial column on both tables (nullable first so the
            // column can be added to tables that already hold rows).
            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Destinations",
                type: "geometry",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "Branches",
                type: "geometry",
                nullable: true);

            // Convert any existing Lat/Long values into Point geometry
            // (X = longitude, Y = latitude) before dropping the old columns.
            migrationBuilder.Sql(
                "UPDATE \"Destinations\" SET \"Location\" = ST_SetSRID(ST_MakePoint(\"Long\", \"Lat\"), 4326) " +
                "WHERE \"Lat\" IS NOT NULL AND \"Long\" IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE \"Branches\" SET \"Location\" = ST_SetSRID(ST_MakePoint(\"Long\", \"Lat\"), 4326) " +
                "WHERE \"Lat\" IS NOT NULL AND \"Long\" IS NOT NULL;");

            // Enforce NOT NULL now that every row has a Location.
            migrationBuilder.Sql("ALTER TABLE \"Destinations\" ALTER COLUMN \"Location\" SET NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Branches\" ALTER COLUMN \"Location\" SET NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "Long",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Long",
                table: "Branches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Lat",
                table: "Destinations",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Long",
                table: "Destinations",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Lat",
                table: "Branches",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Long",
                table: "Branches",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            // Restore Lat/Long from the geometry column before dropping it.
            migrationBuilder.Sql(
                "UPDATE \"Destinations\" SET \"Lat\" = ST_Y(\"Location\"), \"Long\" = ST_X(\"Location\");");
            migrationBuilder.Sql(
                "UPDATE \"Branches\" SET \"Lat\" = ST_Y(\"Location\"), \"Long\" = ST_X(\"Location\");");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Branches");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
