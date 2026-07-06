using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class FixNullables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "description",
                table: "Destinations",
                newName: "Description");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Destinations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Visits",
                table: "Destinations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "One of the Seven Wonders of the Ancient World.", "Active", 18200 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "The iconic limestone statue on the Giza Plateau.", "Active", 15600 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "The largest ancient religious site in the world.", "Active", 12450 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "Royal burial ground of pharaohs from the New Kingdom era.", "Active", 10300 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "Rock-cut temples of Ramesses II on the shores of Lake Nasser.", "Active", 8900 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "A 15th-century defensive fortress in Alexandria.", "Active", 7200 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "Home to the world's largest collection of ancient Egyptian artifacts.", "Active", 22100 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "One of the oldest Christian monasteries in the world.", "Active", 4500 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "A remote oasis home to the Oracle Temple of Amun.", "Active", 3200 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "UNESCO World Heritage Site with fossils of ancient whales.", "Pending", 1800 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "One of Egypt's best-preserved temples dedicated to Hathor.", "Active", 5600 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "The forgotten pharaonic capital hiding undiscovered royal treasures.", "Pending", 900 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "Home to the Bent Pyramid and Red Pyramid built by Pharaoh Sneferu.", "Active", 4100 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "The world's largest archaeological museum with over 100,000 artifacts.", "Active", 9800 });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Description", "Status", "Visits" },
                values: new object[] { "A medieval Islamic fortification built by Saladin in the 12th century.", "Inactive", 6700 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Destinations");

            migrationBuilder.DropColumn(
                name: "Visits",
                table: "Destinations");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Destinations",
                newName: "description");

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 3,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 4,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 5,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 6,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 7,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 8,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 9,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 10,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 11,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 12,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 13,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 14,
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 15,
                column: "description",
                value: null);
        }
    }
}
