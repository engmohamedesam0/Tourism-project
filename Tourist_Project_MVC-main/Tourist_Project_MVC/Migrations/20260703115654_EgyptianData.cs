using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class EgyptianData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "admin-user-id",
                columns: new[] { "Email", "NormalizedEmail" },
                values: new object[] { "admin@egyxplore.com", "ADMIN@EGYXPLORE.COM" });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Category", "City", "Lat", "Long", "Name", "TicketPrice", "description" },
                values: new object[] { "Archaeological", "Giza", 29.9792f, 31.1342f, "The Great Pyramids of Giza", 200.00m, null });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Category", "City", "Lat", "Long", "Name", "TicketPrice", "description" },
                values: new object[] { "Archaeological", "Giza", 29.9753f, 31.1376f, "The Great Sphinx", 0.00m, null });

            migrationBuilder.InsertData(
                table: "Destinations",
                columns: new[] { "Id", "Category", "City", "Lat", "Long", "Name", "OpeningHours", "TicketPrice", "description" },
                values: new object[,]
                {
                    { 3, "Temple", "Luxor", 25.7188f, 32.6573f, "Karnak Temple Complex", null, 150.00m, null },
                    { 4, "Archaeological", "Luxor", 25.7402f, 32.6014f, "Valley of the Kings", null, 180.00m, null },
                    { 5, "Temple", "Aswan", 22.3372f, 31.6258f, "Abu Simbel Temples", null, 220.00m, null },
                    { 6, "Historical", "Alexandria", 31.2141f, 29.8858f, "Qaitbay Citadel", null, 40.00m, null },
                    { 7, "Museum", "Cairo", 30.0478f, 31.2336f, "Egyptian Museum", null, 100.00m, null },
                    { 8, "Religious", "South Sinai", 28.5569f, 33.9758f, "Saint Catherine's Monastery", null, 50.00m, null },
                    { 9, "Natural", "Matrouh", 29.2031f, 25.5195f, "Siwa Oasis", null, 30.00m, null },
                    { 10, "Natural", "Fayoum", 29.2711f, 30.0389f, "Wadi El Hitan (Whale Valley)", null, 25.00m, null },
                    { 11, "Temple", "Qena", 26.1415f, 32.6697f, "Dendera Temple", null, 80.00m, null },
                    { 12, "Archaeological", "Sharqia", 30.9769f, 31.8731f, "Tanis (Ancient City)", null, 20.00m, null },
                    { 13, "Archaeological", "Giza", 29.8086f, 31.2214f, "Dahshur Pyramids", null, 60.00m, null },
                    { 14, "Museum", "Giza", 29.9884f, 31.1188f, "The Grand Egyptian Museum", null, 250.00m, null },
                    { 15, "Historical", "Cairo", 30.0287f, 31.2599f, "Saladin Citadel", null, 60.00m, null }
                });

            migrationBuilder.UpdateData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "PointsReward", "Title" },
                values: new object[] { "Capture a panoramic photo of all three pyramids at sunset", 150, "Pyramid Panorama" });

            migrationBuilder.UpdateData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "DestinationId", "PointsReward", "Title" },
                values: new object[] { "Visit at least 5 different tombs inside the Valley of the Kings", 4, 200, "Valley Explorer" });

            migrationBuilder.UpdateData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "PointsRedeemed", "RedemptionDate", "TouristId" },
                values: new object[] { "MARRIOTT15-EGY", 200, new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 2 });

            migrationBuilder.UpdateData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "PointsRequired", "SponsorId", "Title" },
                values: new object[] { "Get 15% discount on your next stay at Cairo Marriott Hotel", 200, 1, "15% Off Marriott Cairo" });

            migrationBuilder.UpdateData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "ExpirationDate", "PointsRequired", "QuantityAvailable", "RewardType", "SponsorId", "Title" },
                values: new object[] { "Complimentary business class upgrade on domestic EgyptAir flights", new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 500, 10, "Ticket", 2, "Free EgyptAir Upgrade" });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "ContactNumber", "Name", "Type" },
                values: new object[] { "16 Saray El Gezira St, Zamalek, Cairo", 223456789, "Cairo Marriott Hotel", "Hotel" });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Address", "ContactNumber", "Name", "Type" },
                values: new object[] { "Cairo International Airport, Cairo", 290777000, "EgyptAir", "Airline" });

            migrationBuilder.InsertData(
                table: "Sponsors",
                columns: new[] { "Id", "Address", "ContactNumber", "Name", "Type" },
                values: new object[,]
                {
                    { 3, "26 Tahrir Square, Downtown Cairo", 222756000, "Emeco Travel", "Tourism Agency" },
                    { 4, "Corniche El Nile, Luxor", 953580422, "Sofitel Luxor Winter Palace", "Hotel" },
                    { 5, "Elephantine Island, Aswan", 972780222, "Hilton Aswan", "Hotel" }
                });

            migrationBuilder.UpdateData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "point_Balance" },
                values: new object[] { "ahmed.hassan@email.com", "EG123456789", "Ahmed Hassan", "Egyptian", null, "HashedPass123", new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 350 });

            migrationBuilder.UpdateData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "point_Balance" },
                values: new object[] { "james.wilson@email.com", null, "James Wilson", "American", "US987654321", "HashedPass456", new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 120 });

            migrationBuilder.InsertData(
                table: "Tourists",
                columns: new[] { "Id", "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "point_Balance" },
                values: new object[,]
                {
                    { 3, "sophie.muller@email.com", null, "Sophie Müller", "German", "DE456789123", "HashedPass789", new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 200 },
                    { 4, "yuki.tanaka@email.com", null, "Yuki Tanaka", "Japanese", "JP321654987", "HashedPassABC", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), 80 },
                    { 5, "mohamed.ali@email.com", "EG987654321", "Mohamed Ali", "Egyptian", null, "HashedPassXYZ", new DateTime(2026, 1, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 500 }
                });

            migrationBuilder.UpdateData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ArrivalDate", "DepartureDate" },
                values: new object[] { new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ArrivalDate", "DepartureDate", "DestinationId" },
                values: new object[] { new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 7 });

            migrationBuilder.UpdateData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate", "Title", "TouristId" },
                values: new object[] { new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Classic Egypt Tour", 2 });

            migrationBuilder.InsertData(
                table: "TripPlans",
                columns: new[] { "Id", "EndDate", "StartDate", "Title", "TouristId" },
                values: new object[] { 3, new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Nile Valley Explorer", 1 });

            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Completed_At", "PointsEarned", "TouristId" },
                values: new object[] { new DateTime(2026, 8, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), 150, 2 });

             migrationBuilder.UpdateData(
                    table: "UserMissions",
                    keyColumn: "Id",
                    keyValue: 2,
                    columns: new[] { "MissionId", "TouristId" },
                    values: new object[] { 2, 2 });

            migrationBuilder.InsertData(
                table: "UserMissions",
                columns: new[] { "Id", "Completed_At", "MissionId", "PointsEarned", "Status", "TouristId" },
                values: new object[] { 3, new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 200, "Completed", 1 });

            migrationBuilder.InsertData(
                table: "Missions",
                columns: new[] { "Id", "Description", "DestinationId", "MissionType", "PointsReward", "Title" },
                values: new object[,]
                {
                    { 3, "Attend the Sound and Light Show at Karnak Temple", 3, "Cultural", 100, "Karnak Night Show" },
                    { 4, "Witness the solar alignment phenomenon at Abu Simbel Temple", 5, "Adventure", 300, "Sun Alignment Witness" },
                    { 5, "Explore the Oracle Temple of Amun in Siwa Oasis", 9, "Discovery", 250, "Hidden Oasis Quest" },
                    { 6, "Photograph 10 different whale fossils in Wadi El Hitan", 10, "Photography", 180, "Whale Valley Fossils" },
                    { 7, "Locate and photograph 5 artifacts from Tutankhamun's collection in the Egyptian Museum", 7, "Cultural", 120, "Tutankhamun Treasure Hunt" }
                });

            migrationBuilder.InsertData(
                table: "Rewards",
                columns: new[] { "Id", "Description", "ExpirationDate", "PointsRequired", "QuantityAvailable", "RewardType", "SponsorId", "Title" },
                values: new object[,]
                {
                    { 3, "Complimentary one-day Nile cruise provided by Emeco Travel", new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 300, 20, "Tour", 3, "Free Nile Cruise Day" },
                    { 4, "Enjoy 20% off at the historic Sofitel Luxor Winter Palace Hotel", new DateTime(2027, 3, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 250, 30, "Discount", 4, "20% Off Luxor Winter Palace" },
                    { 5, "Exclusive Nile-view sunset dinner for two at Hilton Aswan", new DateTime(2027, 1, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 400, 15, "Experience", 5, "Hilton Aswan Sunset Dinner" }
                });

            migrationBuilder.InsertData(
                table: "TripDestinations",
                columns: new[] { "Id", "ArrivalDate", "DepartureDate", "DestinationId", "TripPlanId", "Visit_Order" },
                values: new object[,]
                {
                    { 3, new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, 1, 3 },
                    { 4, new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 1, 4 },
                    { 8, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 14, 3, 1 },
                    { 9, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 3, 2 },
                    { 10, new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, 3, 3 }
                });

            migrationBuilder.InsertData(
                table: "TripPlans",
                columns: new[] { "Id", "EndDate", "StartDate", "Title", "TouristId" },
                values: new object[] { 2, new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Hidden Wonders of Egypt", 3 });

            migrationBuilder.InsertData(
                table: "Redemptions",
                columns: new[] { "Id", "Code", "PointsRedeemed", "RedemptionDate", "RewardId", "Status", "TouristId" },
                values: new object[] { 2, "NILE-CRUISE-EMC", 300, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Used", 1 });

            migrationBuilder.InsertData(
                table: "TripDestinations",
                columns: new[] { "Id", "ArrivalDate", "DepartureDate", "DestinationId", "TripPlanId", "Visit_Order" },
                values: new object[,]
                {
                    { 5, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), 9, 2, 1 },
                    { 6, new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), 10, 2, 2 },
                    { 7, new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 11, 2, 3 }
                });

            migrationBuilder.InsertData(
                table: "UserMissions",
                columns: new[] { "Id", "Completed_At", "MissionId", "PointsEarned", "Status", "TouristId" },
                values: new object[] { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, 0, "In Progress", 4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "admin-user-id",
                columns: new[] { "Email", "NormalizedEmail" },
                values: new object[] { "admin@yourteam.com", "ADMIN@YOURTEAM.COM" });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Category", "City", "Lat", "Long", "Name", "TicketPrice", "description" },
                values: new object[] { null, "Paris", 48.8584f, 2.2945f, "Eiffel Tower", null, "Iconic iron tower." });

            migrationBuilder.UpdateData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Category", "City", "Lat", "Long", "Name", "TicketPrice", "description" },
                values: new object[] { null, "Rome", 41.8902f, 12.4922f, "Colosseum", null, "Ancient Roman amphitheater." });

            migrationBuilder.UpdateData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "PointsReward", "Title" },
                values: new object[] { "Take a photo at the top of the Eiffel Tower", 100, "Parisian Explorer" });

            migrationBuilder.UpdateData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "DestinationId", "PointsReward", "Title" },
                values: new object[] { "Visit the underground chambers of the Colosseum", 2, 120, "Gladiator Walk" });

            migrationBuilder.UpdateData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Code", "PointsRedeemed", "RedemptionDate", "TouristId" },
                values: new object[] { "ECO10-XYZ", 100, new DateTime(2026, 6, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 1 });

            migrationBuilder.UpdateData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "PointsRequired", "SponsorId", "Title" },
                values: new object[] { "Get 10% off your next stay at EcoStay", 100, 2, "10% Hotel Discount" });

            migrationBuilder.UpdateData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "ExpirationDate", "PointsRequired", "QuantityAvailable", "RewardType", "SponsorId", "Title" },
                values: new object[] { "Complimentary walking tour by Global Travel Co.", new DateTime(2026, 11, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 80, 20, "Experience", 1, "Free City Tour" });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "ContactNumber", "Name", "Type" },
                values: new object[] { "123 Main St, London", 5550192, "Global Travel Co.", "Agency" });

            migrationBuilder.UpdateData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Address", "ContactNumber", "Name", "Type" },
                values: new object[] { "456 Green Rd, Berlin", 5550143, "EcoStay Hotels", "Hospitality" });

            migrationBuilder.UpdateData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "point_Balance" },
                values: new object[] { "john.doe@example.com", "US123456", "John Doe", "American", "P987654", "HashedPassword123", new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), 150 });

            migrationBuilder.UpdateData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "point_Balance" },
                values: new object[] { "jane.smith@example.com", "UK789101", "Jane Smith", "British", "P112233", "HashedPassword456", new DateTime(2026, 2, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), 50 });

            migrationBuilder.UpdateData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ArrivalDate", "DepartureDate" },
                values: new object[] { new DateTime(2026, 7, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 7, 6, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "ArrivalDate", "DepartureDate", "DestinationId" },
                values: new object[] { new DateTime(2026, 7, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 2 });

            migrationBuilder.UpdateData(
                table: "TripPlans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EndDate", "StartDate", "Title", "TouristId" },
                values: new object[] { new DateTime(2026, 7, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Summer Europe Trip", 1 });

            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Completed_At", "PointsEarned", "TouristId" },
                values: new object[] { new DateTime(2026, 6, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 100, 1 });

            migrationBuilder.UpdateData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "MissionId", "TouristId" },
                values: new object[] { 2, 2 });
        }
    }
}
