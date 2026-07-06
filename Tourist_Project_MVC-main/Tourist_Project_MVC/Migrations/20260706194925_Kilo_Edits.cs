using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tourist_Project_MVC.Migrations
{
    /// <inheritdoc />
    public partial class Kilo_Edits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpeningHours = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lat = table.Column<float>(type: "real", nullable: false),
                    Long = table.Column<float>(type: "real", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TicketPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Rating = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Visits = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tourists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Passport = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    point_Balance = table.Column<int>(type: "int", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tourists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Missions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MissionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsReward = table.Column<int>(type: "int", nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Missions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Missions_Destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Destinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RewardType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsRequired = table.Column<int>(type: "int", nullable: false),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SponsorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rewards_Sponsors_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Sponsors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TouristId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripPlans_Tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "Tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsEarned = table.Column<int>(type: "int", nullable: false),
                    Completed_At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TouristId = table.Column<int>(type: "int", nullable: false),
                    MissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMissions_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMissions_Tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "Tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Redemptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PointsRedeemed = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedemptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RewardId = table.Column<int>(type: "int", nullable: false),
                    TouristId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Redemptions_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Redemptions_Tourists_TouristId",
                        column: x => x.TouristId,
                        principalTable: "Tourists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripDestinations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Visit_Order = table.Column<int>(type: "int", nullable: false),
                    ArrivalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TripPlanId = table.Column<int>(type: "int", nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripDestinations_Destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Destinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripDestinations_TripPlans_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "TripPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { "admin-user-id", 0, "STATIC-CONCURRENCY-12345", "admin@egyxplore.com", false, false, null, "ADMIN@EGYXPLORE.COM", "ADMIN", "AQAAAAIAAYagAAAAEKAL8njrbJvg9ETwynEH//f1WRUeqjGkQwDjyymt3nZ80AjWGoDryl5K+MtnAPrRuw==", null, false, "STATIC-STAMP-12345", false, "admin" });

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
                columns: new[] { "Id", "Address", "ContactNumber", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "16 Saray El Gezira St, Zamalek, Cairo", 223456789, "Cairo Marriott Hotel", "Hotel" },
                    { 2, "Cairo International Airport, Cairo", 290777000, "EgyptAir", "Airline" },
                    { 3, "26 Tahrir Square, Downtown Cairo", 222756000, "Emeco Travel", "Tourism Agency" },
                    { 4, "Corniche El Nile, Luxor", 953580422, "Sofitel Luxor Winter Palace", "Hotel" },
                    { 5, "Elephantine Island, Aswan", 972780222, "Hilton Aswan", "Hotel" }
                });

            migrationBuilder.InsertData(
                table: "Tourists",
                columns: new[] { "Id", "Email", "IdNumber", "Name", "Nationality", "Passport", "Password", "RegisterDate", "Status", "point_Balance" },
                values: new object[,]
                {
                    { 1, "ahmed.hassan@email.com", "EG123456789", "Ahmed Hassan", "Egyptian", null, "HashedPass123", new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 350 },
                    { 2, "james.wilson@email.com", null, "James Wilson", "American", "US987654321", "HashedPass456", new DateTime(2026, 2, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 120 },
                    { 3, "sophie.muller@email.com", null, "Sophie Müller", "German", "DE456789123", "HashedPass789", new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 200 },
                    { 4, "yuki.tanaka@email.com", null, "Yuki Tanaka", "Japanese", "JP321654987", "HashedPassABC", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 80 },
                    { 5, "mohamed.ali@email.com", "EG987654321", "Mohamed Ali", "Egyptian", null, "HashedPassXYZ", new DateTime(2026, 1, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", 500 }
                });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { "role-admin-id", "admin-user-id" });

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
                table: "Rewards",
                columns: new[] { "Id", "Description", "ExpirationDate", "PointsRequired", "QuantityAvailable", "RewardType", "SponsorId", "Title" },
                values: new object[,]
                {
                    { 1, "Get 15% discount on your next stay at Cairo Marriott Hotel", new DateTime(2027, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 200, 50, "Discount", 1, "15% Off Marriott Cairo" },
                    { 2, "Complimentary business class upgrade on domestic EgyptAir flights", new DateTime(2027, 6, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), 500, 10, "Ticket", 2, "Free EgyptAir Upgrade" },
                    { 3, "Complimentary one-day Nile cruise provided by Emeco Travel", new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 300, 20, "Tour", 3, "Free Nile Cruise Day" },
                    { 4, "Enjoy 20% off at the historic Sofitel Luxor Winter Palace Hotel", new DateTime(2027, 3, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 250, 30, "Discount", 4, "20% Off Luxor Winter Palace" },
                    { 5, "Exclusive Nile-view sunset dinner for two at Hilton Aswan", new DateTime(2027, 1, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), 400, 15, "Experience", 5, "Hilton Aswan Sunset Dinner" }
                });

            migrationBuilder.InsertData(
                table: "TripPlans",
                columns: new[] { "Id", "EndDate", "StartDate", "Status", "Title", "TouristId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 8, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Classic Egypt Tour", 2 },
                    { 2, new DateTime(2026, 9, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Hidden Wonders of Egypt", 3 },
                    { 3, new DateTime(2026, 10, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Active", "Nile Valley Explorer", 1 }
                });

            migrationBuilder.InsertData(
                table: "Redemptions",
                columns: new[] { "Id", "Code", "PointsRedeemed", "RedemptionDate", "RewardId", "Status", "TouristId" },
                values: new object[,]
                {
                    { 1, "MARRIOTT15-EGY", 200, new DateTime(2026, 8, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Active", 2 },
                    { 2, "NILE-CRUISE-EMC", 300, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Used", 1 }
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

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_DestinationId",
                table: "Missions",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_RewardId",
                table: "Redemptions",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_TouristId",
                table: "Redemptions",
                column: "TouristId");

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_SponsorId",
                table: "Rewards",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_DestinationId",
                table: "TripDestinations",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripPlanId",
                table: "TripDestinations",
                column: "TripPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPlans_TouristId",
                table: "TripPlans",
                column: "TouristId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMissions_MissionId",
                table: "UserMissions",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMissions_TouristId",
                table: "UserMissions",
                column: "TouristId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Redemptions");

            migrationBuilder.DropTable(
                name: "TripDestinations");

            migrationBuilder.DropTable(
                name: "UserMissions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "TripPlans");

            migrationBuilder.DropTable(
                name: "Missions");

            migrationBuilder.DropTable(
                name: "Sponsors");

            migrationBuilder.DropTable(
                name: "Tourists");

            migrationBuilder.DropTable(
                name: "Destinations");
        }
    }
}
