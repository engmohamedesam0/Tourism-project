using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class SponsorDashboardRedemptionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Redemptions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RewardViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RewardId = table.Column<int>(type: "int", nullable: false),
                    TouristId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ViewedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardViews_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "BranchId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "BranchId",
                value: 3);

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_BranchId",
                table: "Redemptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardViews_RewardId",
                table: "RewardViews",
                column: "RewardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Redemptions_Branches_BranchId",
                table: "Redemptions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Redemptions_Branches_BranchId",
                table: "Redemptions");

            migrationBuilder.DropTable(
                name: "RewardViews");

            migrationBuilder.DropIndex(
                name: "IX_Redemptions_BranchId",
                table: "Redemptions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Redemptions");
        }
    }
}
