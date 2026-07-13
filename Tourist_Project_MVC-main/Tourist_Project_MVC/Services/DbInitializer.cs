using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services
{
    /// <summary>
    /// Idempotent, JSON-driven seed routine. All sample data lives in the
    /// SeedData/ folder as plain JSON; this class loads it and inserts it into
    /// the database the first time the app starts (or whenever a table is empty).
    /// Re-running is safe: every table is guarded by an Any() check, and each
    /// table is inserted in FK-dependency order so references always resolve.
    /// </summary>
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider services)
        {
            try
            {
                InitializeAsync(services).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Seeding must never crash application startup. Log and continue.
                Console.Error.WriteLine($"[DbInitializer] Seeding failed: {ex.Message}");
            }
        }

        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TouristContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

            var seedDir = Path.Combine(env.ContentRootPath, "SeedData");

            // 1. Identity roles + users (UserManager / RoleManager).
            await EnsureRolesAsync(roleManager);
            await SeedUsersAsync(userManager, seedDir);

            // 2. Application tables, in FK-dependency order.
            await SeedTableAsync<Sponsor>(context, seedDir, "sponsors.json");
            await SeedTableAsync<Tourist>(context, seedDir, "tourists.json");
            await SeedTableAsync<Destination>(context, seedDir, "destinations.json");
            await SeedTableAsync<Branch>(context, seedDir, "branches.json");
            await SeedTableAsync<MenuItem>(context, seedDir, "menu-items.json");
            await SeedTableAsync<Mission>(context, seedDir, "missions.json");
            await SeedTableAsync<Reward>(context, seedDir, "rewards.json");
            await SeedTableAsync<RewardBranch>(context, seedDir, "reward-branches.json");
            await SeedTableAsync<Redemption>(context, seedDir, "redemptions.json");
            await SeedTableAsync<Review>(context, seedDir, "reviews.json");
            await SeedTableAsync<RewardView>(context, seedDir, "reward-views.json");
            await SeedTableAsync<TripPlan>(context, seedDir, "trip-plans.json");
            await SeedTableAsync<TripDestination>(context, seedDir, "trip-destinations.json");
            await SeedTableAsync<UserMission>(context, seedDir, "user-missions.json");
            await SeedTableAsync<Notification>(context, seedDir, "notifications.json");
            await SeedTableAsync<SupportTicket>(context, seedDir, "support-tickets.json");
            await SeedTableAsync<SponsorApprovalRequest>(context, seedDir, "sponsor-approval-requests.json");
        }

        private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in new[] { "Admin", "User", "Sponsor" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, string seedDir)
        {
            var users = ReadJson<List<SeedUser>>(Path.Combine(seedDir, "users.json"));
            if (users == null)
            {
                return;
            }

            foreach (var u in users)
            {
                if (await userManager.FindByEmailAsync(u.Email) != null)
                {
                    continue;
                }

                var user = new ApplicationUser
                {
                    Id = u.Id,
                    UserName = string.IsNullOrWhiteSpace(u.UserName) ? u.Email : u.UserName,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Nationality = u.Nationality
                };

                var result = await userManager.CreateAsync(user, u.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, u.Role);
                }
                else
                {
                    Console.Error.WriteLine(
                        $"[DbInitializer] Could not create user {u.Email}: " +
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        private static async Task SeedTableAsync<TEntity>(TouristContext context, string seedDir, string fileName)
            where TEntity : class
        {
            var set = context.Set<TEntity>();
            if (await set.AnyAsync())
            {
                return;
            }

            var entities = ReadJson<List<TEntity>>(Path.Combine(seedDir, fileName));
            if (entities == null || entities.Count == 0)
            {
                return;
            }

            var entityType = context.Model.FindEntityType(typeof(TEntity))!;
            var tableName = entityType.GetTableName()!;
            var primaryKey = entityType.FindPrimaryKey()!;
            var identityProperty = primaryKey.Properties.FirstOrDefault(p => p.ValueGenerated.HasFlag(ValueGenerated.OnAdd));

            await using var transaction = await context.Database.BeginTransactionAsync();

            set.AddRange(entities);
            await context.SaveChangesAsync();

            if (identityProperty != null)
                {
                     #pragma warning disable EF1002
                var columnName = identityProperty.GetColumnName();
                await context.Database.ExecuteSqlRawAsync(
                $"SELECT setval(pg_get_serial_sequence('\"{tableName}\"', '{columnName}'), " +
                $"COALESCE((SELECT MAX(\"{columnName}\") FROM \"{tableName}\"), 1))");
                #pragma warning restore EF1002
                }

            await transaction.CommitAsync();
        }

        private static T? ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        private class SeedUser
        {
            public string Id { get; set; } = "";
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string Nationality { get; set; } = "";
            public string Email { get; set; } = "";
            public string? UserName { get; set; }
            public string Password { get; set; } = "";
            public string Role { get; set; } = "User";
        }
    }
}
