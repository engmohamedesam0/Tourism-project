using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Data
{
    public class TouristContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Tourist> Tourists { get; set; }
        public DbSet<TripPlan> TripPlans { get; set; }
        public DbSet<Destination> Destinations { get; set; }
        public DbSet<TripDestination> TripDestinations { get; set; }
        public DbSet<Sponsor> Sponsors { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Redemption> Redemptions { get; set; }
        public DbSet<Mission> Missions { get; set; }
        public DbSet<UserMission> UserMissions { get; set; }

        public TouristContext(DbContextOptions<TouristContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Destination>()
                .Property(d => d.TicketPrice)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<Destination>()
                .Property(d => d.Rating)
                .HasColumnType("decimal(18, 2)");

            // Link Tourist -> ApplicationUser (Identity login). Nullable FK:
            // Tourists created directly by an Admin may not have a login account.
            modelBuilder.Entity<Tourist>()
                .Property(t => t.ApplicationUserId)
                .HasMaxLength(450);

            modelBuilder.Entity<Tourist>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.ApplicationUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // 1. Seed Destinations
            modelBuilder.Entity<Destination>().HasData(
                 new
                 {
                     Id = 1,
                     Name = "The Great Pyramids of Giza",
                     City = "Giza",
                     Category = "Archaeological",
                     Lat = 29.9792f,
                     Long = 31.1342f,
                      Description = "One of the Seven Wonders of the Ancient World.",
                      TicketPrice = 200.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.8m,
                      Tags = "UNESCO, Wonder, Pyramid",
                      Visits = 18200
                 },
                 new
                 {
                     Id = 2,
                     Name = "The Great Sphinx",
                     City = "Giza",
                     Category = "Archaeological",
                     Lat = 29.9753f,
                     Long = 31.1376f,
                      Description = "The iconic limestone statue on the Giza Plateau.",
                      TicketPrice = 0.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.7m,
                      Tags = "Monument, Statue, Free Entry",
                      Visits = 15600
                 },
                 new
                 {
                     Id = 3,
                     Name = "Karnak Temple Complex",
                     City = "Luxor",
                     Category = "Temple",
                     Lat = 25.7188f,
                     Long = 32.6573f,
                      Description = "The largest ancient religious site in the world.",
                      TicketPrice = 150.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.6m,
                      Tags = "Temple, Religious, Columns",
                      Visits = 12450
                 },
                 new
                 {
                     Id = 4,
                     Name = "Valley of the Kings",
                     City = "Luxor",
                     Category = "Archaeological",
                     Lat = 25.7402f,
                     Long = 32.6014f,
                      Description = "Royal burial ground of pharaohs from the New Kingdom era.",
                      TicketPrice = 180.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.7m,
                      Tags = "Tomb, Archaeological, Royal",
                      Visits = 10300
                 },
                 new
                 {
                     Id = 5,
                     Name = "Abu Simbel Temples",
                     City = "Aswan",
                     Category = "Temple",
                     Lat = 22.3372f,
                     Long = 31.6258f,
                      Description = "Rock-cut temples of Ramesses II on the shores of Lake Nasser.",
                      TicketPrice = 220.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.9m,
                      Tags = "Temple, Ramses, UNESCO",
                      Visits = 8900
                 },
                 new
                 {
                     Id = 6,
                     Name = "Qaitbay Citadel",
                     City = "Alexandria",
                     Category = "Historical",
                     Lat = 31.2141f,
                     Long = 29.8858f,
                      Description = "A 15th-century defensive fortress in Alexandria.",
                      TicketPrice = 40.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.3m,
                      Tags = "Fortress, Historical, Sea View",
                      Visits = 7200
                 },
                 new
                 {
                     Id = 7,
                     Name = "Egyptian Museum",
                     City = "Cairo",
                     Category = "Museum",
                     Lat = 30.0478f,
                     Long = 31.2336f,
                      Description = "Home to the world's largest collection of ancient Egyptian artifacts.",
                      TicketPrice = 100.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.5m,
                      Tags = "Museum, Artifacts, Mummies",
                      Visits = 22100
                 },
                 new
                 {
                     Id = 8,
                     Name = "Saint Catherine's Monastery",
                     City = "South Sinai",
                     Category = "Religious",
                     Lat = 28.5569f,
                     Long = 33.9758f,
                      Description = "One of the oldest Christian monasteries in the world.",
                      TicketPrice = 50.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.4m,
                      Tags = "Monastery, Religious, Mountain",
                      Visits = 4500
                 },
                 new
                 {
                     Id = 9,
                     Name = "Siwa Oasis",
                     City = "Matrouh",
                     Category = "Natural",
                     Lat = 29.2031f,
                     Long = 25.5195f,
                      Description = "A remote oasis home to the Oracle Temple of Amun.",
                      TicketPrice = 30.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.6m,
                      Tags = "Oasis, Nature, Spring",
                      Visits = 3200
                 },
                 new
                 {
                     Id = 10,
                     Name = "Wadi El Hitan (Whale Valley)",
                     City = "Fayoum",
                     Category = "Natural",
                     Lat = 29.2711f,
                     Long = 30.0389f,
                      Description = "UNESCO World Heritage Site with fossils of ancient whales.",
                      TicketPrice = 25.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Pending",
                      Rating = 4.2m,
                      Tags = "Fossils, Nature, Desert",
                      Visits = 1800
                 },
                 new
                 {
                     Id = 11,
                     Name = "Dendera Temple",
                     City = "Qena",
                     Category = "Temple",
                     Lat = 26.1415f,
                     Long = 32.6697f,
                      Description = "One of Egypt's best-preserved temples dedicated to Hathor.",
                      TicketPrice = 80.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.5m,
                      Tags = "Temple, Hathor, Ceiling",
                      Visits = 5600
                 },
                 new
                 {
                     Id = 12,
                     Name = "Tanis (Ancient City)",
                     City = "Sharqia",
                     Category = "Archaeological",
                     Lat = 30.9769f,
                     Long = 31.8731f,
                      Description = "The forgotten pharaonic capital hiding undiscovered royal treasures.",
                      TicketPrice = 20.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Pending",
                      Rating = 4.0m,
                      Tags = "Ruins, Archaeological, Hidden",
                      Visits = 900
                 },
                 new
                 {
                     Id = 13,
                     Name = "Dahshur Pyramids",
                     City = "Giza",
                     Category = "Archaeological",
                     Lat = 29.8086f,
                     Long = 31.2214f,
                      Description = "Home to the Bent Pyramid and Red Pyramid built by Pharaoh Sneferu.",
                      TicketPrice = 60.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.3m,
                      Tags = "Pyramid, Archaeological, Quiet",
                      Visits = 4100
                 },
                 new
                 {
                     Id = 14,
                     Name = "The Grand Egyptian Museum",
                     City = "Giza",
                     Category = "Museum",
                     Lat = 29.9884f,
                     Long = 31.1188f,
                      Description = "The world's largest archaeological museum with over 100,000 artifacts.",
                      TicketPrice = 250.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Active",
                      Rating = 4.9m,
                      Tags = "Museum, Modern, Tutankhamun",
                      Visits = 9800
                 },
                 new
                 {
                     Id = 15,
                     Name = "Saladin Citadel",
                     City = "Cairo",
                     Category = "Historical",
                     Lat = 30.0287f,
                     Long = 31.2599f,
                      Description = "A medieval Islamic fortification built by Saladin in the 12th century.",
                      TicketPrice = 60.00m,
                      OpeningHours = (DateTime?)null,
                      Status = "Inactive",
                      Rating = 4.2m,
                      Tags = "Fortress, Historical, Mosque",
                      Visits = 6700
                 }
             );

            // 2. Seed Sponsors
            modelBuilder.Entity<Sponsor>().HasData(
                 new
                 {
                     Id = 1,
                     Name = "Cairo Marriott Hotel",
                     Type = "Hotel",
                     Address = "16 Saray El Gezira St, Zamalek, Cairo",
                     ContactNumber = 223456789
                 },
                 new
                 {
                     Id = 2,
                     Name = "EgyptAir",
                     Type = "Airline",
                     Address = "Cairo International Airport, Cairo",
                     ContactNumber = 290777000
                 },
                 new
                 {
                     Id = 3,
                     Name = "Emeco Travel",
                     Type = "Tourism Agency",
                     Address = "26 Tahrir Square, Downtown Cairo",
                     ContactNumber = 222756000
                 },
                 new
                 {
                     Id = 4,
                     Name = "Sofitel Luxor Winter Palace",
                     Type = "Hotel",
                     Address = "Corniche El Nile, Luxor",
                     ContactNumber = 953580422
                 },
                 new
                 {
                     Id = 5,
                     Name = "Hilton Aswan",
                     Type = "Hotel",
                     Address = "Elephantine Island, Aswan",
                     ContactNumber = 972780222
                 }
             );

            // 3. Seed Tourists
            modelBuilder.Entity<Tourist>().HasData(
                new
                {
                    Id = 1,
                    Name = "Ahmed Hassan",
                    Nationality = "Egyptian",
                    IdNumber = "EG123456789",
                    Passport = (string?)null,
                    Email = "ahmed.hassan@email.com",
                    Password = "HashedPass123",
                    point_Balance = 350,
                    Status = "Active",
                    RegisterDate = DateTime.Parse("2026-01-10")
                },
                new
                {
                    Id = 2,
                    Name = "James Wilson",
                    Nationality = "American",
                    IdNumber = (string?)null,
                    Passport = "US987654321",
                    Email = "james.wilson@email.com",
                    Password = "HashedPass456",
                    point_Balance = 120,
                    Status = "Active",
                    RegisterDate = DateTime.Parse("2026-02-15")
                },
                new
                {
                    Id = 3,
                    Name = "Sophie Müller",
                    Nationality = "German",
                    IdNumber = (string?)null,
                    Passport = "DE456789123",
                    Email = "sophie.muller@email.com",
                    Password = "HashedPass789",
                    point_Balance = 200,
                    Status = "Active",
                    RegisterDate = DateTime.Parse("2026-03-01")
                },
                new
                {
                    Id = 4,
                    Name = "Yuki Tanaka",
                    Nationality = "Japanese",
                    IdNumber = (string?)null,
                    Passport = "JP321654987",
                    Email = "yuki.tanaka@email.com",
                    Password = "HashedPassABC",
                    point_Balance = 80,
                    Status = "Active",
                    RegisterDate = DateTime.Parse("2026-03-20")
                },
                new
                {
                    Id = 5,
                    Name = "Mohamed Ali",
                    Nationality = "Egyptian",
                    IdNumber = "EG987654321",
                    Passport = (string?)null,
                    Email = "mohamed.ali@email.com",
                    Password = "HashedPassXYZ",
                    point_Balance = 500,
                    Status = "Active",
                    RegisterDate = DateTime.Parse("2026-01-05")
                }
            );

            // 4. Seed Missions
            modelBuilder.Entity<Mission>().HasData(
                new
                {
                    Id = 1,
                    MissionType = "Photography",
                    Title = "Pyramid Panorama",
                    Description = "Capture a panoramic photo of all three pyramids at sunset",
                    PointsReward = 150,
                    DestinationId = 1
                },
                new
                {
                    Id = 2,
                    MissionType = "Exploration",
                    Title = "Valley Explorer",
                    Description = "Visit at least 5 different tombs inside the Valley of the Kings",
                    PointsReward = 200,
                    DestinationId = 4
                },
                new
                {
                    Id = 3,
                    MissionType = "Cultural",
                    Title = "Karnak Night Show",
                    Description = "Attend the Sound and Light Show at Karnak Temple",
                    PointsReward = 100,
                    DestinationId = 3
                },
                new
                {
                    Id = 4,
                    MissionType = "Adventure",
                    Title = "Sun Alignment Witness",
                    Description = "Witness the solar alignment phenomenon at Abu Simbel Temple",
                    PointsReward = 300,
                    DestinationId = 5
                },
                new
                {
                    Id = 5,
                    MissionType = "Discovery",
                    Title = "Hidden Oasis Quest",
                    Description = "Explore the Oracle Temple of Amun in Siwa Oasis",
                    PointsReward = 250,
                    DestinationId = 9
                },
                new
                {
                    Id = 6,
                    MissionType = "Photography",
                    Title = "Whale Valley Fossils",
                    Description = "Photograph 10 different whale fossils in Wadi El Hitan",
                    PointsReward = 180,
                    DestinationId = 10
                },
                new
                {
                    Id = 7,
                    MissionType = "Cultural",
                    Title = "Tutankhamun Treasure Hunt",
                    Description = "Locate and photograph 5 artifacts from Tutankhamun's collection in the Egyptian Museum",
                    PointsReward = 120,
                    DestinationId = 7
                }
            );

            // 5. Seed Rewards
            modelBuilder.Entity<Reward>().HasData(
                new
                {
                    Id = 1,
                    RewardType = "Discount",
                    Title = "15% Off Marriott Cairo",
                    Description = "Get 15% discount on your next stay at Cairo Marriott Hotel",
                    PointsRequired = 200,
                    QuantityAvailable = 50,
                    ExpirationDate = DateTime.Parse("2027-12-31"),
                    SponsorId = 1
                },
                new
                {
                    Id = 2,
                    RewardType = "Ticket",
                    Title = "Free EgyptAir Upgrade",
                    Description = "Complimentary business class upgrade on domestic EgyptAir flights",
                    PointsRequired = 500,
                    QuantityAvailable = 10,
                    ExpirationDate = DateTime.Parse("2027-06-30"),
                    SponsorId = 2
                },
                new
                {
                    Id = 3,
                    RewardType = "Tour",
                    Title = "Free Nile Cruise Day",
                    Description = "Complimentary one-day Nile cruise provided by Emeco Travel",
                    PointsRequired = 300,
                    QuantityAvailable = 20,
                    ExpirationDate = DateTime.Parse("2026-12-31"),
                    SponsorId = 3
                },
                new
                {
                    Id = 4,
                    RewardType = "Discount",
                    Title = "20% Off Luxor Winter Palace",
                    Description = "Enjoy 20% off at the historic Sofitel Luxor Winter Palace Hotel",
                    PointsRequired = 250,
                    QuantityAvailable = 30,
                    ExpirationDate = DateTime.Parse("2027-03-31"),
                    SponsorId = 4
                },
                new
                {
                    Id = 5,
                    RewardType = "Experience",
                    Title = "Hilton Aswan Sunset Dinner",
                    Description = "Exclusive Nile-view sunset dinner for two at Hilton Aswan",
                    PointsRequired = 400,
                    QuantityAvailable = 15,
                    ExpirationDate = DateTime.Parse("2027-01-31"),
                    SponsorId = 5
                }
            );

            // 6. Seed Trip Plans
            modelBuilder.Entity<TripPlan>().HasData(
                new
                {
                    Id = 1,
                    Title = "Classic Egypt Tour",
                    StartDate = DateTime.Parse("2026-08-01"),
                    EndDate = DateTime.Parse("2026-08-10"),
                    Status = "Active",
                    TouristId = 2
                },
                new
                {
                    Id = 2,
                    Title = "Hidden Wonders of Egypt",
                    StartDate = DateTime.Parse("2026-09-05"),
                    EndDate = DateTime.Parse("2026-09-12"),
                    Status = "Active",
                    TouristId = 3
                },
                new
                {
                    Id = 3,
                    Title = "Nile Valley Explorer",
                    StartDate = DateTime.Parse("2026-10-01"),
                    EndDate = DateTime.Parse("2026-10-08"),
                    Status = "Active",
                    TouristId = 1
                }
            );

            // 7. Seed Trip Destinations
            modelBuilder.Entity<TripDestination>().HasData(
                new
                {
                    Id = 1,
                    Visit_Order = 1,
                    ArrivalDate = DateTime.Parse("2026-08-01"),
                    DepartureDate = DateTime.Parse("2026-08-03"),
                    TripPlanId = 1,
                    DestinationId = 1 
                },
                new
                {
                    Id = 2,
                    Visit_Order = 2,
                    ArrivalDate = DateTime.Parse("2026-08-03"),
                    DepartureDate = DateTime.Parse("2026-08-05"),
                    TripPlanId = 1,
                    DestinationId = 7 
                },
                new
                {
                    Id = 3,
                    Visit_Order = 3,
                    ArrivalDate = DateTime.Parse("2026-08-05"),
                    DepartureDate = DateTime.Parse("2026-08-08"),
                    TripPlanId = 1,
                    DestinationId = 3  
                },
                new
                {
                    Id = 4,
                    Visit_Order = 4,
                    ArrivalDate = DateTime.Parse("2026-08-08"),
                    DepartureDate = DateTime.Parse("2026-08-10"),
                    TripPlanId = 1,
                    DestinationId = 5  
                },

                new
                {
                    Id = 5,
                    Visit_Order = 1,
                    ArrivalDate = DateTime.Parse("2026-09-05"),
                    DepartureDate = DateTime.Parse("2026-09-07"),
                    TripPlanId = 2,
                    DestinationId = 9 
                },
                new
                {
                    Id = 6,
                    Visit_Order = 2,
                    ArrivalDate = DateTime.Parse("2026-09-07"),
                    DepartureDate = DateTime.Parse("2026-09-09"),
                    TripPlanId = 2,
                    DestinationId = 10  
                },
                new
                {
                    Id = 7,
                    Visit_Order = 3,
                    ArrivalDate = DateTime.Parse("2026-09-09"),
                    DepartureDate = DateTime.Parse("2026-09-12"),
                    TripPlanId = 2,
                    DestinationId = 11  
                },

                new
                {
                    Id = 8,
                    Visit_Order = 1,
                    ArrivalDate = DateTime.Parse("2026-10-01"),
                    DepartureDate = DateTime.Parse("2026-10-03"),
                    TripPlanId = 3,
                    DestinationId = 14  
                },
                new
                {
                    Id = 9,
                    Visit_Order = 2,
                    ArrivalDate = DateTime.Parse("2026-10-03"),
                    DepartureDate = DateTime.Parse("2026-10-05"),
                    TripPlanId = 3,
                    DestinationId = 4   
                },
                new
                {
                    Id = 10,
                    Visit_Order = 3,
                    ArrivalDate = DateTime.Parse("2026-10-05"),
                    DepartureDate = DateTime.Parse("2026-10-08"),
                    TripPlanId = 3,
                    DestinationId = 3   
                }
            );
            // 8. Seed User Missions

            modelBuilder.Entity<UserMission>().HasData(
                new
                {
                    Id = 1,
                    Status = "Completed",
                    PointsEarned = 150,
                    Completed_At = DateTime.Parse("2026-08-02"),
                    TouristId = 2,
                    MissionId = 1  
                },
                new
                {
                    Id = 2,
                    Status = "In Progress",
                    PointsEarned = 0,
                    Completed_At = DateTime.MinValue,
                    TouristId = 3,
                    MissionId = 2  
                },
                new
                {
                    Id = 3,
                    Status = "Completed",
                    PointsEarned = 200,
                    Completed_At = DateTime.Parse("2026-10-04"),
                    TouristId = 1,
                    MissionId = 2 
                },
                new
                {
                    Id = 4,
                    Status = "In Progress",
                    PointsEarned = 0,
                    Completed_At = DateTime.MinValue,
                    TouristId = 4,
                    MissionId = 1 
                }
            );

            // 9. Seed Redemptions
            
            modelBuilder.Entity<Redemption>().HasData(
                new
                {
                    Id = 1,
                    Code = "MARRIOTT15-EGY",
                    PointsRedeemed = 200,
                    Status = "Active",
                    RedemptionDate = DateTime.Parse("2026-08-05"),
                    RewardId = 1,
                    TouristId = 2
                },
                new
                {
                    Id = 2,
                    Code = "NILE-CRUISE-EMC",
                    PointsRedeemed = 300,
                    Status = "Used",
                    RedemptionDate = DateTime.Parse("2026-10-03"),
                    RewardId = 3,
                    TouristId = 1
                }
            );

            
            // 10. Seed Roles
            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "role-admin-id",
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "STATIC-ROLE-STAMP-1"
                },
                new IdentityRole
                {
                    Id = "role-user-id",
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp = "STATIC-ROLE-STAMP-2"
                }
            );

            
            // 11. Seed Admin User
            modelBuilder.Entity<ApplicationUser>().HasData(
                new ApplicationUser
                {
                    Id = "admin-user-id",
                    UserName = "admin",
                    NormalizedUserName = "ADMIN",
                    Email = "admin@egyxplore.com",
                    NormalizedEmail = "ADMIN@EGYXPLORE.COM",
                    PasswordHash = "AQAAAAIAAYagAAAAEKAL8njrbJvg9ETwynEH//f1WRUeqjGkQwDjyymt3nZ80AjWGoDryl5K+MtnAPrRuw==",
                    SecurityStamp = "STATIC-STAMP-12345",
                    ConcurrencyStamp = "STATIC-CONCURRENCY-12345"
                }
            );

            // 12. Link Admin to Role
            
            modelBuilder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string>
                {
                    RoleId = "role-admin-id",
                    UserId = "admin-user-id"
                }
            );
        }
    }
}