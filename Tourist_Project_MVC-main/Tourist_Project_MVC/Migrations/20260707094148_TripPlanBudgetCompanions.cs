using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class TripPlanBudgetCompanions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Budget",
                table: "TripPlans",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Companions",
                table: "TripPlans",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Budget", "Companions" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Budget", "Companions" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Budget", "Companions" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Budget",
                table: "TripPlans");

            migrationBuilder.DropColumn(
                name: "Companions",
                table: "TripPlans");
        }
    }
}
