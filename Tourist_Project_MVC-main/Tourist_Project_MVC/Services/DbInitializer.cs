using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using System.Net.Http;
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
                var msg = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
                Console.Error.WriteLine($"[DbInitializer] Seeding overall error: {msg}");
            }
        }

        public static async Task InitializeAsync(IServiceProvider services)
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TouristContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();

            var seedDir = Path.Combine(env.ContentRootPath, "SeedData");

            // 1. Identity roles + users (UserManager / RoleManager).
            await EnsureRolesAsync(roleManager);
            await SeedUsersAsync(userManager, seedDir);

            // 2. Application tables, in FK-dependency order.
            await SeedTableAsync<Sponsor>(context, seedDir, "sponsors.json");
            await SeedTableAsync<Tourist>(context, seedDir, "tourists.json");
            await SeedGeoAsync<Branch>(context, seedDir, "branches.json",
                (e, el) => e.Location = new Point(el.GetProperty("lng").GetDouble(), el.GetProperty("lat").GetDouble()) { SRID = 4326 });
            await SeedTableAsync<MenuItem>(context, seedDir, "menu-items.json");
            await SeedGeoAsync<Utility>(context, seedDir, "utilities.json",
                (e, el) => e.Location = new Point(el.GetProperty("lng").GetDouble(), el.GetProperty("lat").GetDouble()) { SRID = 4326 });

            // Pull-sync Destinations from ArcGIS BEFORE seeding dependent tables (Missions, TripDestinations).
            // ArcGIS is the primary source of truth for destination data.
            try
            {
                var arcgisSync = scope.ServiceProvider.GetRequiredService<IArcGISSyncService>();
                var syncResult = await arcgisSync.SyncDestinationsFromArcGIS(CancellationToken.None);

                if (!syncResult.Success)
                {
                    Console.Error.WriteLine($"[DbInitializer] Destinations ArcGIS pull-sync failed: {syncResult.Error}");
                }

                var branchSyncResult = await arcgisSync.SyncBranchesFromArcGIS(CancellationToken.None);
                if (!branchSyncResult.Success)
                {
                    Console.Error.WriteLine($"[DbInitializer] Branches ArcGIS pull-sync failed: {branchSyncResult.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DbInitializer] ArcGIS sync warning: {ex.Message}");
            }

            // Only seed from local JSON if the table is completely empty.
            if (!await context.Destinations.AnyAsync())
            {
                Console.WriteLine("[DbInitializer] Seeding destinations from local destinations.json.");
                await SeedGeoAsync<Destination>(context, seedDir, "destinations.json",
                    (e, el) =>
                    {
                        if (el.TryGetProperty("lat", out var lat) && el.TryGetProperty("lng", out var lng))
                            e.Location = new Point(lng.GetDouble(), lat.GetDouble()) { SRID = 4326 };
                    });
            }

            // Sync PostgreSQL ID sequence for Destinations table so any future identity inserts won't conflict.
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT setval(pg_get_serial_sequence('\"Destinations\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"Destinations\"), 1))");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbInitializer] Sequence setval warning: {ex.Message}");
            }

            // Tables dependent on Destinations:
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

            // Gamification tables (after Tourists and Missions are seeded).
            await SeedTableAsync<Badge>(context, seedDir, "badges.json");
            await SeedTableAsync<UserProgress>(context, seedDir, "user-progress.json");
            await SeedTableAsync<UserBadge>(context, seedDir, "user-badges.json");
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

        private static async Task SeedGeoAsync<TEntity>(TouristContext context, string seedDir, string fileName, Action<TEntity, JsonElement> buildLocation)
            where TEntity : class
        {
            try
            {
                var set = context.Set<TEntity>();
                if (await set.AnyAsync())
                {
                    return;
                }

                var path = Path.Combine(seedDir, fileName);
                if (!File.Exists(path))
                {
                    return;
                }

                var json = await File.ReadAllTextAsync(path);
                var elements = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (elements == null || elements.Count == 0)
                {
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entityType = context.Model.FindEntityType(typeof(TEntity))!;
                var tableName = entityType.GetTableName()!;
                var primaryKey = entityType.FindPrimaryKey()!;
                var identityProperty = primaryKey.Properties.FirstOrDefault(p => p.ValueGenerated.HasFlag(ValueGenerated.OnAdd));

                foreach (var el in elements)
                {
                    try
                    {
                        var entity = el.Deserialize<TEntity>(options)!;
                        buildLocation(entity, el);
                        set.Add(entity);
                        await context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        context.ChangeTracker.Clear();
                        Console.Error.WriteLine($"[DbInitializer] Warning seeding item in {typeof(TEntity).Name} ({fileName}): {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                if (identityProperty != null)
                {
                    try
                    {
                        #pragma warning disable EF1002
                        var columnName = identityProperty.GetColumnName();
                        await context.Database.ExecuteSqlRawAsync(
                        $"SELECT setval(pg_get_serial_sequence('\"{tableName}\"', '{columnName}'), " +
                        $"COALESCE((SELECT MAX(\"{columnName}\") FROM \"{tableName}\"), 1))");
                        #pragma warning restore EF1002
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                context.ChangeTracker.Clear();
                var msg = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
                Console.Error.WriteLine($"[DbInitializer] Failed seeding geo {typeof(TEntity).Name} ({fileName}): {msg}");
            }
        }

        private static async Task SeedTableAsync<TEntity>(TouristContext context, string seedDir, string fileName)
            where TEntity : class
        {
            try
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

                foreach (var entity in entities)
                {
                    try
                    {
                        set.Add(entity);
                        await context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        context.ChangeTracker.Clear();
                        Console.Error.WriteLine($"[DbInitializer] Warning seeding item in {typeof(TEntity).Name} ({fileName}): {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                if (identityProperty != null)
                {
                    try
                    {
                        #pragma warning disable EF1002
                        var columnName = identityProperty.GetColumnName();
                        await context.Database.ExecuteSqlRawAsync(
                        $"SELECT setval(pg_get_serial_sequence('\"{tableName}\"', '{columnName}'), " +
                        $"COALESCE((SELECT MAX(\"{columnName}\") FROM \"{tableName}\"), 1))");
                        #pragma warning restore EF1002
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                context.ChangeTracker.Clear();
                var msg = ex.InnerException != null ? $"{ex.Message} --> {ex.InnerException.Message}" : ex.Message;
                Console.Error.WriteLine($"[DbInitializer] Failed seeding {typeof(TEntity).Name} ({fileName}): {msg}");
            }
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
