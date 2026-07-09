using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHasDataSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "role-user-id");

            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { "role-admin-id", "admin-user-id" });

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 2);

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
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 8);

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
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Redemptions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Reviews",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Rewards",
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
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TripDestinations",
                keyColumn: "Id",
                keyValue: 2);

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
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "UserMissions",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "role-admin-id");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "admin-user-id");

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 3);

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
                keyValue: 10);

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
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Missions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rewards",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 2);

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
                keyValue: 1);

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
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Destinations",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Sponsors",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Tourists",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "role-admin-id", "STATIC-ROLE-STAMP-1", "Admin", "ADMIN" },
                    { "role-user-id", "STATIC-ROLE-STAMP-2", "User", "USER" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "FirstName", "LastName", "LockoutEnabled", "LockoutEnd", "Nationality", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicturePath", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { "admin-user-id", 0, "STATIC-CONCURRENCY-12345", "admin@egyxplore.com", false, "", "", false, null, "", "ADMIN@EGYXPLORE.COM", "ADMIN", "AQAAAAIAAYagAAAAEKAL8njrbJvg9ETwynEH//f1WRUeqjGkQwDjyymt3nZ80AjWGoDryl5K+MtnAPrRuw==", null, false, null, "STATIC-STAMP-12345", false, "admin" });

            migrationBuilder.InsertData(
                table: "Destinations",
                columns: new[] { "Id", "Category", "City", "Description", "Lat", "Long", "Name", "OpeningHours", "Rating", "Status", "Tags", "TicketPrice", "Visits" },
                values: new object[,]
                {
                    { 1, "Archaeological", "Giza", "One of the Seven Wonders of the Ancient World.", 29.9792f, 31.1342f, "The Great Pyramids of Giza", null, 4.8m, "Active", "UNESCO, Wonder, Pyramid", 200.00m, 18200 },
                    { 2, "Archaeological", "Giza", "The iconic limestone statue on the Giza Plateau.", 29.9753f, 31.1376f, "The Great Sphinx", null, 4.7m, "Active", "Monument, Statue, Free Entry", 0.00m, 15600 },
                    { 3, "Temple", "Luxor", "The largest ancient religious site in the world.", 25.7188f, 32.6573f, "Karnak Temple Complex", null, 4.6m, "Active", "Temple, Religious, Columns", 150.00m, 12450 },
                    { 4, "Archaeological", "Luxor", "Royal burial ground of pharaohs from the New Kingdom era.", 25.7402f, 32.6014f, "Valley of the Kings", null, 4.7m, "Active", "Tomb, Archaeological, Royal", 180.00m, 10300 },
                    { 5, "Temple", "Aswan", "Rock-cut temples of Ramesses II on the shores of Lake Nasser.", 22.3372f, 31.6258f, "Abu Simbel Temples", null, 4.9m, "Active", "Temple, Ramses, UNESCO", 220.00m, 8900 },
                    { 6, "Historical", "Alexandria", "A 15th-century defensive fortress in Alexandria.", 31.2141f, 29.8858f, "Qaitbay Citadel", null, 4.3m, "Active", "Fortress, Historical, Sea View", 40.00m, 7200 },
                    { 7, "Museum", "Cairo", "Home to the world's largest collection of ancient Egyptian artifacts.", 30.0478f, 31.2336f, "Egyptian Museum", null, 4.5m, "Active", "Museum, Artifacts, Mummies", 100.00m, 22100 },
                    { 8, "Religious", "South Sinai", "One of the oldest Christian monasteries in the world.", 28.5569f, 33.9758f, "Saint Catherine's Monastery", null, 4.4m, "Active", "Monastery, Religious, Mountain", 50.00m, 4500 },
                    { 9, "Natural", "Matrouh", "A remote oasis home to the Oracle Temple of Amun.", 29.2031f, 25.5195f, "Siwa Oasis", null, 4.6m, "Active", "Oasis, Nature, Spring", 30.00m, 3200 },
                    { 10, "Natural", "Fayoum", "UNESCO World Heritage Site with fossils of ancient whales.", 29.2711f, 30.0389f, "Wadi El Hitan (Whale Valley)", null, 4.2m, "Pending", "Fossils, Nature, Desert", 25.00m, 1800 },
                    { 11, "Temple", "Qena", "One of Egypt's best-preserved temples dedicated to Hathor.", 26.1415f, 32.6697f, "Dendera Temple", null, 4.5m, "Active", "Temple, Hathor, Ceiling", 80.00m, 5600 },
                    { 12, "Archaeological", "Sharqia", "The forgotten pharaonic capital hiding undiscovered royal treasures.", 30.9769f, 31.8731f, "Tanis (Ancient City)", null, 4.0m, "Pending", "Ruins, Archaeological, Hidden", 20.00m, 900 },
                    { 13, "Archaeological", "Giza", "Home to the Bent Pyramid and Red Pyramid built by Pharaoh Sneferu.", 29.8086f, 31.2214f, "Dahshur Pyramids", null, 4.3m, "Active", "Pyramid, Archaeological, Quiet", 60.00m, 4100 },
                    { 14, "Museum", "Giza", "The world's largest archaeological museum with over 100,000 artifacts.", 29.9884f, 31.1188f, "The Grand Egyptian Museum", null, 4.9m, "Active", "Museum, Modern, Tutankhamun", 250.00m, 9800 },
                    { 15, "Historical", "Cairo", "A medieval Islamic fortification built by Saladin in the 12th century.", 30.0287f, 31.2599f, "Saladin Citadel", null, 4.2m, "Inactive", "Fortress, Historical, Mosque", 60.00m, 6700 }
                });

            migrationBuilder.InsertData(
                table: "Sponsors",
                columns: new[] { "Id", "Address", "ApplicationUserId", "ContactNumber", "Email", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "16 Saray El Gezira St, Zamalek, Cairo", null, 223456789, null, "Cairo Marriott Hotel", "Hotel" },
                    { 2, "Cairo International Airport, Cairo", null, 290777000, null, "EgyptAir", "Airline" },
                    { 3, "26 Tahrir Square, Downtown Cairo", null, 222756000, null, "Emeco Travel", "Tourism Agency" },
                    { 4, "Corniche El Nile, Luxor", null, 953580422, null, "Sofitel Luxor Winter Palace", "Hotel" },
                    { 5, "Elephantine Island, Aswan", null, 972780222, null, "Hilton Aswan", "Hotel" }
                });

            migrationBuilder.InsertData(
                table: "Tourists",
                columns: new[] { "Id", "ApplicationUserId", "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "Status", "point_Balance" },
                values: new object[,]
                {
                    { 1, null, "ahmed.hassan@email.com", "EG123456789", "Ahmed Hassan", "Egyptian", null, "HashedPass123", new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 350 },
                    { 2, null, "james.wilson@email.com", null, "James Wilson", "American", "US987654321", "HashedPass456", new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 120 },
                    { 3, null, "sophie.muller@email.com", null, "Sophie Müller", "German", "DE456789123", "HashedPass789", new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 200 },
                    { 4, null, "yuki.tanaka@email.com", null, "Yuki Tanaka", "Japanese", "JP321654987", "HashedPassABC", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 80 },
                    { 5, null, "mohamed.ali@email.com", "EG987654321", "Mohamed Ali", "Egyptian", null, "HashedPassXYZ", new DateTime(2026, 1, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 500 }
                });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { "role-admin-id", "admin-user-id" });

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

            migrationBuilder.InsertData(
                table: "MenuItems",
                columns: new[] { "Id", "Description", "Name", "Price", "SponsorId" },
                values: new object[,]
                {
                    { 1, "Extensive international & Egyptian breakfast with terrace view.", "Nile View Breakfast Buffet", 25.00m, 1 },
                    { 2, "Signature Lebanese & Egyptian set-menu dinner.", "Omar Khayyam Oriental Dinner", 45.00m, 1 },
                    { 3, "Two-hour traditional sailboat cruise at sunset.", "Nile Felucca Sunset Tour", 30.00m, 3 },
                    { 4, "Guided visit to Pyramids, Sphinx and Egyptian Museum.", "Cairo City Day Tour", 60.00m, 3 },
                    { 5, "Colonial-style tea service in the historic gardens.", "Winter Palace Royal Afternoon Tea", 18.00m, 4 },
                    { 6, "Three-course dinner overlooking the Nile.", "Nile Terrace Set Menu", 38.00m, 4 },
                    { 7, "Fresh Nile perch grilled with local spices.", "Aswanian Fish Grill", 22.00m, 5 },
                    { 8, "Evening pool access with a three-course dinner.", "Sunset Pool & Dinner Pass", 40.00m, 5 }
                });

            migrationBuilder.InsertData(
                table: "Missions",
                columns: new[] { "Id", "Description", "DestinationId", "MissionType", "PointsReward", "Title" },
                values: new object[,]
                {
                    { 1, "Capture a panoramic photo of all three pyramids at sunset", 1, "Photography", 150, "Pyramid Panorama" },
                    { 2, "Visit at least 5 different tombs inside the Valley of the Kings", 4, "Exploration", 200, "Valley Explorer" },
                    { 3, "Attend the Sound and Light Show at Karnak Temple", 3, "Cultural", 100, "Karnak Night Show" },
                    { 4, "Witness the solar alignment phenomenon at Abu Simbel Temple", 5, "Adventure", 300, "Sun Alignment Witness" },
                    { 5, "Explore the Oracle Temple of Amun in Siwa Oasis", 9, "Discovery", 250, "Hidden Oasis Quest" },
                    { 6, "Photograph 10 different whale fossils in Wadi El Hitan", 10, "Photography", 180, "Whale Valley Fossils" },
                    { 7, "Locate and photograph 5 artifacts from Tutankhamun's collection in the Egyptian Museum", 7, "Cultural", 120, "Tutankhamun Treasure Hunt" }
                });

            migrationBuilder.InsertData(
                table: "Reviews",
                columns: new[] { "Id", "Comment", "CreatedDate", "Rating", "SponsorId", "TouristId" },
                values: new object[,]
                {
                    { 1, "Incredible views of the Nile and top-notch service.", new DateTime(2026, 4, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 1, 2 },
                    { 2, "Comfortable rooms, a bit pricey but worth it.", new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 1, 3 },
                    { 3, "Smooth flight and friendly cabin crew.", new DateTime(2026, 4, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 2, 4 },
                    { 4, "Great guided tour, very knowledgeable guide.", new DateTime(2026, 5, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 3, 1 },
                    { 5, "Historic atmosphere and beautiful gardens.", new DateTime(2026, 3, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 4, 4 },
                    { 6, "Lovely sunset dinner by the water.", new DateTime(2026, 4, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 5, 2 }
                });

            migrationBuilder.InsertData(
                table: "Rewards",
                columns: new[] { "Id", "Description", "ExpirationDate", "PointsRequired", "QuantityAvailable", "RewardType", "SponsorId", "Status", "Title" },
                values: new object[,]
                {
                    { 1, "Get 15% discount on your next stay at Cairo Marriott Hotel", new DateTime(2027, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 200, 50, "Discount", 1, "Active", "15% Off Marriott Cairo" },
                    { 2, "Complimentary business class upgrade on domestic EgyptAir flights", new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 500, 10, "Ticket", 2, "Active", "Free EgyptAir Upgrade" },
                    { 3, "Complimentary one-day Nile cruise provided by Emeco Travel", new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 300, 20, "Tour", 3, "Active", "Free Nile Cruise Day" },
                    { 4, "Enjoy 20% off at the historic Sofitel Luxor Winter Palace Hotel", new DateTime(2027, 3, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 250, 30, "Discount", 4, "Active", "20% Off Luxor Winter Palace" },
                    { 5, "Exclusive Nile-view sunset dinner for two at Hilton Aswan", new DateTime(2027, 1, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 400, 15, "Experience", 5, "Active", "Hilton Aswan Sunset Dinner" }
                });

            migrationBuilder.InsertData(
                table: "TripPlans",
                columns: new[] { "Id", "Budget", "Companions", "EndDate", "StartDate", "Status", "Title", "TouristId" },
                values: new object[,]
                {
                    { 1, null, null, new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Classic Egypt Tour", 2 },
                    { 2, null, null, new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Hidden Wonders of Egypt", 3 },
                    { 3, null, null, new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Nile Valley Explorer", 1 }
                });

            migrationBuilder.InsertData(
                table: "Redemptions",
                columns: new[] { "Id", "BranchId", "Code", "PointsRedeemed", "RedemptionDate", "RewardId", "Status", "TouristId" },
                values: new object[,]
                {
                    { 1, 1, "MARRIOTT15-EGY", 200, new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Active", 2 },
                    { 2, 3, "NILE-CRUISE-EMC", 300, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Used", 1 }
                });

            migrationBuilder.InsertData(
                table: "TripDestinations",
                columns: new[] { "Id", "ArrivalDate", "DepartureDate", "DestinationId", "TripPlanId", "Visit_Order" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 1, 1 },
                    { 2, new DateTime(2026, 8, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 1, 2 },
                    { 3, new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, 1, 3 },
                    { 4, new DateTime(2026, 8, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, 1, 4 },
                    { 5, new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), 9, 2, 1 },
                    { 6, new DateTime(2026, 9, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), 10, 2, 2 },
                    { 7, new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), 11, 2, 3 },
                    { 8, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 14, 3, 1 },
                    { 9, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, 3, 2 },
                    { 10, new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, 3, 3 }
                });

            migrationBuilder.InsertData(
                table: "UserMissions",
                columns: new[] { "Id", "Completed_At", "MissionId", "PointsEarned", "Status", "TouristId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 150, "Completed", 2 },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 0, "In Progress", 3 },
                    { 3, new DateTime(2026, 10, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, 200, "Completed", 1 },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, 0, "In Progress", 4 }
                });
        }
    }
}
