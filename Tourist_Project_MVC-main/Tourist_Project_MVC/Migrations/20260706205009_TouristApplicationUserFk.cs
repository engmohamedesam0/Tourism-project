using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class TouristApplicationUserFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Tourists",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tourists_ApplicationUserId",
                table: "Tourists",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tourists_AspNetUsers_ApplicationUserId",
                table: "Tourists",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tourists_AspNetUsers_ApplicationUserId",
                table: "Tourists");

            migrationBuilder.DropIndex(
                name: "IX_Tourists_ApplicationUserId",
                table: "Tourists");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Tourists");
        }
    }
}
