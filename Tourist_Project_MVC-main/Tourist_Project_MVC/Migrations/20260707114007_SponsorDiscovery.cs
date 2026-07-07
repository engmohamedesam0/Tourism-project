using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class SponsorDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Lat",
                table: "Sponsors",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Long",
                table: "Sponsors",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TouristId = table.Column<int>(type: "int", nullable: false),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "Tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MenuItems",
                columns: new[] { "Id", "Description", "Name", "Price", "SponsorId" },
                values: new object[,]
                {
                    { 1, "Extensive international & Egyptian breakfast with terrace view.", "Nile View Breakfast Buffet", 25.00m, 1 },
                    { 2, "Signature Lebanese & Egyptian set-menu dinner.", "Omar Khayyam Oriental Dinner", 45.00m, 1 },
                    { 3, "Two-hour traditional sailboat cruise at sunset.", "Nile Felucca Sunset Tour", 30.00m, 3 },
                    { 4, "Guided visit to Pyramids, Sphinx and Egyptian Museum.", "Cairo City Day Tour", 60.00m, 3 },
                    { 5, "Colonial-style tea service in the historic gardens.", "Winter Palace Royal Afternoon Tea", 18.00m, 4 },
                    { 6, "Three-course dinner overlooking the Nile.", "Nile Terrace Set Menu", 38.00m, 4 },
                    { 7, "Fresh Nile perch grilled with local spices.", "Aswanian Fish Grill", 22.00m, 5 },
                    { 8, "Evening pool access with a three-course dinner.", "Sunset Pool & Dinner Pass", 40.00m, 5 }
                });

            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "Id", "Comment", "CreatedDate", "Rating", "SponsorId", "TouristId" },
                values: new object[,]
                {
                    { 1, "Incredible views of the Nile and top-notch service.", new DateTime(2026, 4, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 1, 2 },
                    { 2, "Comfortable rooms, a bit pricey but worth it.", new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 1, 3 },
                    { 3, "Smooth flight and friendly cabin crew.", new DateTime(2026, 4, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 2, 4 },
                    { 4, "Great guided tour, very knowledgeable guide.", new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 3, 1 },
                    { 5, "Historic atmosphere and beautiful gardens.", new DateTime(2026, 3, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 4, 4 },
                    { 6, "Lovely sunset dinner by the water.", new DateTime(2026, 4, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 5, 2 }
                });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Lat", "Long" },
                values: new object[] { 30.0669f, 31.2243f });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Lat", "Long" },
                values: new object[] { 30.1118f, 31.4056f });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Lat", "Long" },
                values: new object[] { 30.0444f, 31.2358f });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Lat", "Long" },
                values: new object[] { 25.6989f, 32.6394f });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Lat", "Long" },
                values: new object[] { 24.0822f, 32.8872f });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_SponsorId",
                table: "MenuItems",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_SponsorId",
                table: "Reviews",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TouristId",
                table: "Reviews",
                column: "TouristId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Long",
                table: "Sponsors");
        }
    }
}
