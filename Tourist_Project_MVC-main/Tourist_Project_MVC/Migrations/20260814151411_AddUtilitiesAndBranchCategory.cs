using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddUtilitiesAndBranchCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Branches",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill existing branches with their sponsor's category so the
            // new column is consistent with the data already in the database.
            migrationBuilder.Sql("""
                UPDATE "Branches" b
                SET "Category" = s."Type"
                FROM "Sponsors" s
                WHERE b."SponsorId" = s."Id";
                """);

            migrationBuilder.CreateTable(
                name: "Utilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    ContactNumber = table.Column<string>(type: "text", nullable: true),
                    OpenHours = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<Point>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilities", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Utilities");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Branches");
        }
    }
}
