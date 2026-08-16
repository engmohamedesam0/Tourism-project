using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Generic review system: allow tourists to review Missions too.
            // SiteReview already targets Destination / TripPlan / Reward / Branch
            // via separate nullable FK columns; this migration adds the Mission
            // FK plus a last-updated timestamp for the review row itself.
            migrationBuilder.AddColumn<int>(
                name: "MissionId",
                table: "SiteReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "UpdatedDate",
                table: "SiteReviews",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteReviews_MissionId",
                table: "SiteReviews",
                column: "MissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SiteReviews_Missions_MissionId",
                table: "SiteReviews",
                column: "MissionId",
                principalTable: "Missions",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SiteReviews_Missions_MissionId",
                table: "SiteReviews");

            migrationBuilder.DropIndex(
                name: "IX_SiteReviews_MissionId",
                table: "SiteReviews");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "SiteReviews");

            migrationBuilder.DropColumn(
                name: "MissionId",
                table: "SiteReviews");
        }
    }
}
