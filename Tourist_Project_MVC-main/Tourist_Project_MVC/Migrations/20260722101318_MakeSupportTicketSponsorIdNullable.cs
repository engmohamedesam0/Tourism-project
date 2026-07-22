using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class MakeSupportTicketSponsorIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Sponsors_SponsorId",
                table: "SupportTickets");

            migrationBuilder.AlterColumn<int>(
                name: "SponsorId",
                table: "SupportTickets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "SponsorRespondedDate",
                table: "SupportTickets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SponsorResponse",
                table: "SupportTickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Sponsors_SponsorId",
                table: "SupportTickets",
                column: "SponsorId",
                principalTable: "Sponsors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Sponsors_SponsorId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SponsorRespondedDate",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SponsorResponse",
                table: "SupportTickets");

            migrationBuilder.AlterColumn<int>(
                name: "SponsorId",
                table: "SupportTickets",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Sponsors_SponsorId",
                table: "SupportTickets",
                column: "SponsorId",
                principalTable: "Sponsors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
