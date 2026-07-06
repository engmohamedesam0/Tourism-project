using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class EgyptianDataa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 2,
                column: "MissionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 4,
                column: "MissionId",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 2,
                column: "MissionId",
                value: 5);

            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 4,
                column: "MissionId",
                value: 6);
        }
    }
}
