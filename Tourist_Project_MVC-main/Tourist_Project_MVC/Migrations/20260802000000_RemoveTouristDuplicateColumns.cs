using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTouristDuplicateColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Tourists");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Tourists");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Tourists");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Tourists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Tourists",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Tourists",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Tourists",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Tourists",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
