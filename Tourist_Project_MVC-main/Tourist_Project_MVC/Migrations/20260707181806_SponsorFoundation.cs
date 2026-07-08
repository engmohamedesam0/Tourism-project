using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class SponsorFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Sponsors_SponsorId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "Long",
                table: "Sponsors");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Sponsors",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lat = table.Column<float>(type: "real", nullable: false),
                    Long = table.Column<float>(type: "real", nullable: false),
                    ContactNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardBranches",
                columns: table => new
                {
                    RewardId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardBranches", x => new { x.RewardId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_RewardBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardBranches_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "ContactNumber", "Lat", "Long", "Name", "SponsorId" },
                values: new object[,]
                {
                    { 1, "16 Saray El Gezira St, Zamalek, Cairo", 223456789, 30.0669f, 31.2243f, "Cairo Marriott Hotel — Main", 1 },
                    { 2, "Cairo International Airport, Cairo", 290777000, 30.1118f, 31.4056f, "EgyptAir — HQ", 2 },
                    { 3, "26 Tahrir Square, Downtown Cairo", 222756000, 30.0444f, 31.2358f, "Emeco Travel — Downtown", 3 },
                    { 4, "Corniche El Nile, Luxor", 953580422, 25.6989f, 32.6394f, "Sofitel Luxor Winter Palace — Main", 4 },
                    { 5, "Elephantine Island, Aswan", 972780222, 24.0822f, 32.8872f, "Hilton Aswan — Main", 5 }
                });

            migrationBuilder.UpdateData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 5,
                column: "TouristId",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 1,
                column: "ApplicationUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 2,
                column: "ApplicationUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 3,
                column: "ApplicationUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 4,
                column: "ApplicationUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 5,
                column: "ApplicationUserId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_ApplicationUserId",
                table: "Sponsors",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_SponsorId",
                table: "Branches",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardBranches_BranchId",
                table: "RewardBranches",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Sponsors_SponsorId",
                table: "Rewards",
                column: "SponsorId",
                principalTable: "Sponsors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Sponsors_AspNetUsers_ApplicationUserId",
                table: "Sponsors",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_Sponsors_SponsorId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Sponsors_AspNetUsers_ApplicationUserId",
                table: "Sponsors");

            migrationBuilder.DropTable(
                name: "RewardBranches");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Sponsors_ApplicationUserId",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Sponsors");

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

            migrationBuilder.UpdateData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 5,
                column: "TouristId",
                value: 5);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_Sponsors_SponsorId",
                table: "Rewards",
                column: "SponsorId",
                principalTable: "Sponsors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
