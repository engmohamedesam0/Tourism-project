using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiTools
{
    /// <summary>
    /// Admin tools — mirror the existing Admin controllers (RewardController,
    /// AdminDashboardController.AddDestination, DestinationController edit/delete,
    /// RoleController.ManageAccounts). Every operation re-uses the application's
    /// own services and authorization rules; nothing is invented here.
    /// </summary>
    public class AdminAiTools
    {
        private static readonly string[] AdminRole = { AiIdentityContext.RoleAdmin };
        private static readonly string[] AllowedRoles = { "User", "Sponsor", "Admin" };

        private readonly TouristContext _context;
        private readonly IRewardRepository _rewardRepo;
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly IArcGISSyncService _arcgisSync;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminAiTools(
            TouristContext context,
            IRewardRepository rewardRepo,
            ISponsorRepository sponsorRepo,
            IDestinationRepository destinationRepo,
            IArcGISSyncService arcgisSync,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _rewardRepo = rewardRepo;
            _sponsorRepo = sponsorRepo;
            _destinationRepo = destinationRepo;
            _arcgisSync = arcgisSync;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public List<AiToolDefinition> Build() => new()
        {
            GetPlatformStats(),
            GetUsersList(),
            GetSponsorsList(),
            ChangeUserRole(),
            CreateReward(),
            UpdateReward(),
            DeleteReward(),
            CreateDestination(),
            UpdateDestination(),
            DeleteDestination()
        };

        // ============================================================
        // get_platform_stats
        // ============================================================
        private AiToolDefinition GetPlatformStats() => new()
        {
            Name = "get_platform_stats",
            Description = "Platform overview for admins: counts of tourists, sponsors, destinations, branches, rewards, redemptions, " +
                          "completed missions, and average review rating. Use when the user asks for an overview of the platform.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = AdminRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var siteReviews = _context.SiteReviews.ToList();
                var stats = new
                {
                    tourists = _context.Tourists.Count(),
                    sponsors = _context.Sponsors.Count(),
                    destinations = _context.Destinations.Count(),
                    branches = _context.Branches.Count(),
                    rewards = _context.Rewards.Count(),
                    redemptions = _context.Redemptions.Count(),
                    missions_completed = _context.UserMissions.Count(um => um.Status == "Completed"),
                    reviews = siteReviews.Count,
                    average_rating = siteReviews.Any() ? Math.Round(siteReviews.Average(r => r.Rating), 2) : (double?)null
                };

                var message =
                    $"Here's the platform overview:\n" +
                    $"- Tourists: {stats.tourists}\n" +
                    $"- Sponsors: {stats.sponsors}\n" +
                    $"- Destinations: {stats.destinations}\n" +
                    $"- Branches: {stats.branches}\n" +
                    $"- Rewards: {stats.rewards}\n" +
                    $"- Redemptions: {stats.redemptions}\n" +
                    $"- Completed missions: {stats.missions_completed}\n" +
                    $"- Reviews: {stats.reviews}" +
                    (stats.average_rating.HasValue ? $" (avg {stats.average_rating.Value:0.0}/5)" : "");

                return Task.FromResult(Ok(message, new { stats }));
            }
        };

        // ============================================================
        // get_users_list
        // ============================================================
        private AiToolDefinition GetUsersList() => new()
        {
            Name = "get_users_list",
            Description = "List user accounts with their current role (optionally filtered by role: User/Sponsor/Admin). " +
                          "Use when the user asks about the platform's users, tourists, or sponsors.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { role = new { type = "STRING", description = "Optional role filter (User, Sponsor, Admin)." } }
            },
            Roles = AdminRole,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<GetUsersArgs>(args);
                var users = _userManager.Users.OrderBy(u => u.Email).Take(100).ToList();

                var rows = new List<AccountSummaryRow>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var role = roles.FirstOrDefault() ?? "—";
                    if (!string.IsNullOrWhiteSpace(parsed?.Role) &&
                        !string.Equals(role, parsed.Role.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;
                    rows.Add(new AccountSummaryRow(
                        user.Email ?? string.Empty,
                        $"{user.FirstName} {user.LastName}".Trim(),
                        role));
                }

                var message = rows.Count == 0
                    ? "No accounts match that filter."
                    : $"Found {rows.Count} account{(rows.Count == 1 ? "" : "s")}:\n" +
                      string.Join("\n", rows.Take(30).Select(r => $"- {r.Email} — {r.Role}"));

                return Ok(message, new { users = rows.Select(r => new { email = r.Email, name = r.Name, role = r.Role }) });
            }
        };

        // ============================================================
        // get_sponsors_list
        // ============================================================
        private AiToolDefinition GetSponsorsList() => new()
        {
            Name = "get_sponsors_list",
            Description = "List all sponsors with their IDs. Use when an admin needs to pick a sponsor (e.g. to create a reward for them).",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = AdminRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsors = _sponsorRepo.GetAll().OrderBy(s => s.Name).ToList();
                var items = sponsors.Select(s => new
                {
                    sponsor_id = s.Id,
                    name = s.Name,
                    type = s.Type,
                    address = s.Address,
                    email = s.Email
                }).ToList();

                var message = sponsors.Count == 0
                    ? "There are no sponsors yet."
                    : $"Here are the sponsors:\n" +
                      string.Join("\n", sponsors.Select(s => $"- id={s.Id} | {s.Name} | {s.Type}"));

                return Task.FromResult(Ok(message, new { sponsors = items }));
            }
        };

        // ============================================================
        // change_user_role
        // ============================================================
        private AiToolDefinition ChangeUserRole() => new()
        {
            Name = "change_user_role",
            Description = "Change the role of a user account (by email) to User (Tourist), Sponsor, or Admin. " +
                          "Mirrors the Manage Accounts page. The admin's OWN role can never be changed this way. " +
                          "The system always asks for confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    user_email = new { type = "STRING", description = "The account's email address." },
                    new_role = new { type = "STRING", description = "New role: User, Sponsor, or Admin." }
                },
                required = new[] { "user_email", "new_role" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<ChangeUserRoleArgs>(args);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.UserEmail) || string.IsNullOrWhiteSpace(parsed.NewRole))
                    return Fail("I need the account's email and the new role (User, Sponsor, or Admin).");

                if (!AllowedRoles.Contains(parsed.NewRole.Trim(), StringComparer.OrdinalIgnoreCase))
                    return Fail("The role must be one of: User, Sponsor, Admin.");

                var user = await _userManager.FindByEmailAsync(parsed.UserEmail.Trim());
                if (user == null)
                    return Fail($"I couldn't find an account with email {parsed.UserEmail.Trim()}.");

                var currentAdmin = await _userManager.GetUserAsync(context.HttpContext.User);
                if (currentAdmin != null && user.Id == currentAdmin.Id)
                    return Fail("You can't change your own role through the assistant.");

                var newRole = AllowedRoles.First(r => string.Equals(r, parsed.NewRole.Trim(), StringComparison.OrdinalIgnoreCase));

                if (context.IsPreview)
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    var from = currentRoles.FirstOrDefault() ?? "no role";
                    return Ok($"I'm ready to change the role of **{user.Email}** from **{from}** to **{newRole}**. Are you sure?");
                }

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Any())
                    await _userManager.RemoveFromRolesAsync(user, roles);
                var result = await _userManager.AddToRoleAsync(user, newRole);

                if (!result.Succeeded)
                    return Fail("I couldn't update that account's role. Please try again.");

                return Ok($"Done! **{user.Email}** is now a **{newRole}**.");
            }
        };

        // ============================================================
        // create_reward (admin — pick sponsor)
        // ============================================================
        private AiToolDefinition CreateReward() => new()
        {
            Name = "create_reward",
            Description = "Create a new reward for a sponsor. Required: sponsor_id (from get_sponsors_list), title, reward_type (e.g. Discount, Voucher), " +
                          "points_required (>= 1), expiration_date (YYYY-MM-DD — the reward's end date; there is no separate start-date field). " +
                          "quantity_available defaults to 0, status defaults to Active. If any required field is missing, ask the user BEFORE calling. " +
                          "The system will ask for confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    sponsor_id = new { type = "INTEGER", description = "The sponsor's ID (from get_sponsors_list)." },
                    title = new { type = "STRING", description = "Reward title (e.g. \"Summer Explorer\")." },
                    reward_type = new { type = "STRING", description = "Reward type (e.g. Discount, Voucher, Gift)." },
                    description = new { type = "STRING", description = "Optional description." },
                    points_required = new { type = "INTEGER", description = "Points required to redeem (at least 1)." },
                    quantity_available = new { type = "INTEGER", description = "Optional quantity available (default 0)." },
                    expiration_date = new { type = "STRING", description = "Expiration/end date (YYYY-MM-DD)." },
                    status = new { type = "STRING", description = "Optional status (Active, Paused, Removed). Default Active." }
                },
                required = new[] { "sponsor_id", "title", "reward_type", "points_required", "expiration_date" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<AdminRewardDraftArgs>(args);
                if (parsed == null || parsed.SponsorId <= 0)
                    return Task.FromResult(Fail("I need a sponsor to create the reward for. Let me list the sponsors."));

                var sponsor = _sponsorRepo.GetById(parsed.SponsorId);
                if (sponsor == null)
                    return Task.FromResult(Fail("That sponsor doesn't exist."));

                if (string.IsNullOrWhiteSpace(parsed.Title) || string.IsNullOrWhiteSpace(parsed.RewardType))
                    return Task.FromResult(Fail("The reward needs a title and a type (e.g. Discount, Voucher)."));
                if (parsed.PointsRequired < 1)
                    return Task.FromResult(Fail("Points required must be at least 1."));

                var expiration = AiToolsCommon.ParseDateOrDefault(parsed.ExpirationDate, DateTime.Today.AddMonths(1));
                var status = string.IsNullOrWhiteSpace(parsed.Status) ? "Active" : parsed.Status.Trim();
                var quantity = parsed.QuantityAvailable ?? 0;

                if (context.IsPreview)
                {
                    var summary =
                        $"I'm ready to create this reward:\n\n" +
                        $"Title: {parsed.Title.Trim()}\n" +
                        $"Type: {parsed.RewardType.Trim()}\n" +
                        (string.IsNullOrWhiteSpace(parsed.Description) ? "" : $"Description: {parsed.Description.Trim()}\n") +
                        $"Points Required: {parsed.PointsRequired}\n" +
                        $"Quantity: {quantity}\n" +
                        $"Expires: {AiToolsCommon.ShortDate(expiration)}\n" +
                        $"Status: {status}\n" +
                        $"Sponsor: {sponsor.Name}";
                    return Task.FromResult(Ok(summary));
                }

                var reward = new Reward
                {
                    Title = parsed.Title.Trim(),
                    RewardType = parsed.RewardType.Trim(),
                    Description = parsed.Description?.Trim() ?? string.Empty,
                    PointsRequired = parsed.PointsRequired,
                    QuantityAvailable = quantity,
                    ExpirationDate = expiration,
                    Status = status,
                    SponsorId = sponsor.Id
                };
                _rewardRepo.Add(reward);
                _rewardRepo.Save();

                var reply = $"Done! The reward **{parsed.Title.Trim()}** has been created successfully for {sponsor.Name} " +
                            $"({parsed.PointsRequired} points, expires {AiToolsCommon.ShortDate(expiration)}).";
                return Task.FromResult(Ok(reply));
            }
        };

        // ============================================================
        // update_reward (admin)
        // ============================================================
        private AiToolDefinition UpdateReward() => new()
        {
            Name = "update_reward",
            Description = "Update an existing reward (reward_id from get_public_rewards or platform data). Only include fields that change. " +
                          "The system will ask for confirmation before saving.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    reward_id = new { type = "INTEGER", description = "The reward's ID." },
                    title = new { type = "STRING", description = "Optional new title." },
                    reward_type = new { type = "STRING", description = "Optional new type." },
                    description = new { type = "STRING", description = "Optional new description." },
                    points_required = new { type = "INTEGER", description = "Optional new points required (>= 1)." },
                    quantity_available = new { type = "INTEGER", description = "Optional new quantity." },
                    expiration_date = new { type = "STRING", description = "Optional new expiration date (YYYY-MM-DD)." },
                    status = new { type = "STRING", description = "Optional new status (Active, Paused, Removed)." }
                },
                required = new[] { "reward_id" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<UpdateRewardArgs>(args);
                if (parsed == null || parsed.RewardId <= 0)
                    return Task.FromResult(Fail("I need a valid reward to update."));

                var reward = _rewardRepo.GetById(parsed.RewardId);
                if (reward == null)
                    return Task.FromResult(Fail("I couldn't find that reward."));

                if (context.IsPreview)
                {
                    var changes = new List<string>();
                    if (!string.IsNullOrWhiteSpace(parsed.Title)) changes.Add($"title → {parsed.Title.Trim()}");
                    if (!string.IsNullOrWhiteSpace(parsed.RewardType)) changes.Add($"type → {parsed.RewardType.Trim()}");
                    if (parsed.Description != null) changes.Add($"description → {parsed.Description.Trim()}");
                    if (parsed.PointsRequired.HasValue) changes.Add($"points → {parsed.PointsRequired.Value}");
                    if (parsed.QuantityAvailable.HasValue) changes.Add($"quantity → {parsed.QuantityAvailable.Value}");
                    if (!string.IsNullOrWhiteSpace(parsed.ExpirationDate)) changes.Add($"expires → {AiToolsCommon.ParseDateOrDefault(parsed.ExpirationDate, reward.ExpirationDate):MMM d, yyyy}");
                    if (!string.IsNullOrWhiteSpace(parsed.Status)) changes.Add($"status → {parsed.Status.Trim()}");
                    if (!changes.Any())
                        return Task.FromResult(Fail("I couldn't see any changes to apply. What would you like to update?"));
                    return Task.FromResult(Ok($"I'm ready to update **{reward.Title}**:\n- {string.Join("\n- ", changes)}"));
                }

                if (!string.IsNullOrWhiteSpace(parsed.Title)) reward.Title = parsed.Title.Trim();
                if (!string.IsNullOrWhiteSpace(parsed.RewardType)) reward.RewardType = parsed.RewardType.Trim();
                if (parsed.Description != null) reward.Description = parsed.Description.Trim();
                if (parsed.PointsRequired.HasValue) reward.PointsRequired = parsed.PointsRequired.Value;
                if (parsed.QuantityAvailable.HasValue) reward.QuantityAvailable = parsed.QuantityAvailable.Value;
                if (!string.IsNullOrWhiteSpace(parsed.ExpirationDate))
                    reward.ExpirationDate = AiToolsCommon.ParseDateOrDefault(parsed.ExpirationDate, reward.ExpirationDate);
                if (!string.IsNullOrWhiteSpace(parsed.Status)) reward.Status = parsed.Status.Trim();

                _rewardRepo.Update(reward);
                _rewardRepo.Save();
                return Task.FromResult(Ok($"Done! The reward **{reward.Title}** has been updated successfully."));
            }
        };

        // ============================================================
        // delete_reward (admin)
        // ============================================================
        private AiToolDefinition DeleteReward() => new()
        {
            Name = "delete_reward",
            Description = "Delete an existing reward (reward_id). Destructive — the system always asks for explicit confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { reward_id = new { type = "INTEGER", description = "The reward's ID." } },
                required = new[] { "reward_id" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<RewardIdArgs>(args);
                if (parsed == null || parsed.RewardId <= 0)
                    return Task.FromResult(Fail("I need a valid reward to delete."));

                var reward = _rewardRepo.GetById(parsed.RewardId);
                if (reward == null)
                    return Task.FromResult(Fail("I couldn't find that reward."));

                if (context.IsPreview)
                {
                    return Task.FromResult(Ok(
                        $"I found the reward **{reward.Title}** ({reward.PointsRequired} points, expires {AiToolsCommon.ShortDate(reward.ExpirationDate)}). " +
                        "Are you sure you want to delete it?"));
                }

                _rewardRepo.Delete(reward.Id);
                _rewardRepo.Save();
                return Task.FromResult(Ok($"Done! The reward **{reward.Title}** has been deleted."));
            }
        };

        // ============================================================
        // create_destination (ArcGIS, mirrors AdminDashboardController.AddDestination)
        // ============================================================
        private AiToolDefinition CreateDestination() => new()
        {
            Name = "create_destination",
            Description = "Create a new destination. Required: name, city, category, and a location (latitude/longitude or a recognizable Egyptian city). " +
                          "Optional: arabic_name, description, tags, ticket_required (Yes/No), prices (egyptian_price, student_egyptian_price, foreign_price, " +
                          "student_foreign_price), image_urls. If category is Public, ticket prices are cleared automatically. " +
                          "Destinations are managed through the platform's ArcGIS service, which must be reachable. " +
                          "The system will ask for confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    name = new { type = "STRING", description = "Destination name (English)." },
                    arabic_name = new { type = "STRING", description = "Optional Arabic name." },
                    city = new { type = "STRING", description = "City/governorate." },
                    category = new { type = "STRING", description = "Category (e.g. Temple, Museum, Beach, Public)." },
                    description = new { type = "STRING", description = "Optional description." },
                    tags = new { type = "STRING", description = "Optional comma-separated tags." },
                    ticket_required = new { type = "STRING", description = "Optional Yes/No." },
                    egyptian_price = new { type = "INTEGER", description = "Optional Egyptian ticket price (EGP)." },
                    student_egyptian_price = new { type = "INTEGER", description = "Optional Egyptian student price (EGP)." },
                    foreign_price = new { type = "INTEGER", description = "Optional foreign ticket price (EGP)." },
                    student_foreign_price = new { type = "INTEGER", description = "Optional foreign student price (EGP)." },
                    latitude = new { type = "NUMBER", description = "Optional latitude." },
                    longitude = new { type = "NUMBER", description = "Optional longitude." },
                    city_for_location = new { type = "STRING", description = "Optional Egyptian city to derive the location from (e.g. Luxor, Giza)." },
                    image_urls = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" },
                        description = "Optional absolute http(s) image URLs."
                    }
                },
                required = new[] { "name", "city", "category" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<AdminDestinationDraftArgs>(args);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.Name) || string.IsNullOrWhiteSpace(parsed.City) || string.IsNullOrWhiteSpace(parsed.Category))
                    return Fail("A destination needs at least a name, a city, and a category.");

                if (!AiToolsCommon.TryResolveLocation(parsed.Latitude, parsed.Longitude, parsed.CityForLocation, out var lat, out var lon))
                    return Fail($"I need a location for this destination — a recognizable Egyptian city or the latitude/longitude.");

                var imageUrls = (parsed.ImageUrls ?? new List<string>())
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Where(url => Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    .Select(url => url.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var isPublic = string.Equals(parsed.Category.Trim(), "Public", StringComparison.OrdinalIgnoreCase);
                var destination = BuildDestinationFromDraft(parsed, lat, lon, imageUrls, isPublic);

                if (context.IsPreview)
                {
                    var summary =
                        $"I'm ready to create this destination:\n\n" +
                        $"Name: {destination.Name}\n" +
                        (string.IsNullOrWhiteSpace(destination.ArabicName) ? "" : $"Arabic Name: {destination.ArabicName}\n") +
                        $"City: {destination.City}\n" +
                        $"Category: {destination.Category}\n" +
                        (string.IsNullOrWhiteSpace(destination.Description) ? "" : $"Description: {destination.Description}\n") +
                        $"Location: ({lat:0.####}, {lon:0.####})\n" +
                        (destination.TicketPrice.HasValue ? $"Ticket Price: {destination.TicketPrice.Value:0.##} EGP\n" : "") +
                        (destination.EgyptianPrice.HasValue ? $"Egyptian Price: {destination.EgyptianPrice} EGP\n" : "") +
                        (destination.ForeignPrice.HasValue ? $"Foreign Price: {destination.ForeignPrice} EGP\n" : "") +
                        (imageUrls.Any() ? $"Images: {imageUrls.Count} image(s)\n" : "");
                    return Ok(summary.TrimEnd());
                }

                // Mirrors AdminDashboardController.AddDestination: create in ArcGIS first.
                var (arcgisSuccess, arcgisError, _, createdId) = await _arcgisSync.AddDestinationToArcGISAsync(destination, ct);
                if (!arcgisSuccess)
                    return Fail($"I couldn't create the destination because the map service is unavailable ({arcgisError}). Nothing was saved.");

                if (createdId.HasValue)
                    destination.Id = createdId.Value;

                try
                {
                    await _arcgisSync.SyncDestinationsFromArcGIS(ct);
                }
                catch (Exception)
                {
                    // local sync best-effort; ArcGIS already has the feature
                }

                return Ok($"Done! The destination **{destination.Name}** has been created successfully.");
            }
        };

        // ============================================================
        // update_destination (ArcGIS, mirrors DestinationController.Edit)
        // ============================================================
        private AiToolDefinition UpdateDestination() => new()
        {
            Name = "update_destination",
            Description = "Update an existing destination (destination_id from search_destinations). Only include fields that change: name, arabic_name, city, " +
                          "category, description, status, tags, prices, open_at, close_at. Changes are pushed through the platform's ArcGIS service. " +
                          "The system will ask for confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    destination_id = new { type = "INTEGER", description = "The destination's ID." },
                    name = new { type = "STRING", description = "Optional new name." },
                    arabic_name = new { type = "STRING", description = "Optional new Arabic name." },
                    city = new { type = "STRING", description = "Optional new city." },
                    category = new { type = "STRING", description = "Optional new category." },
                    description = new { type = "STRING", description = "Optional new description." },
                    status = new { type = "STRING", description = "Optional new status (Active, Pending, Inactive)." },
                    tags = new { type = "STRING", description = "Optional new tags." },
                    egyptian_price = new { type = "INTEGER", description = "Optional new Egyptian price." },
                    student_egyptian_price = new { type = "INTEGER", description = "Optional new Egyptian student price." },
                    foreign_price = new { type = "INTEGER", description = "Optional new foreign price." },
                    student_foreign_price = new { type = "INTEGER", description = "Optional new foreign student price." },
                    open_at = new { type = "INTEGER", description = "Optional new opening hour (0-23)." },
                    close_at = new { type = "INTEGER", description = "Optional new closing hour (0-23)." }
                },
                required = new[] { "destination_id" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<UpdateDestinationArgs>(args);
                if (parsed == null || parsed.DestinationId <= 0)
                    return Fail("I need a valid destination to update.");

                var existing = _destinationRepo.GetById(parsed.DestinationId);
                if (existing == null)
                    return Fail("I couldn't find that destination.");

                var snapshot = await _arcgisSync.GetDestinationSnapshotAsync(parsed.DestinationId, ct);
                if (!snapshot.Success || !snapshot.Records.Any())
                    return Fail("I couldn't reach the destination's map record — please try again later.");

                if (context.IsPreview)
                {
                    var changes = new List<string>();
                    if (!string.IsNullOrWhiteSpace(parsed.Name)) changes.Add($"name → {parsed.Name.Trim()}");
                    if (parsed.ArabicName != null) changes.Add($"Arabic name → {parsed.ArabicName.Trim()}");
                    if (!string.IsNullOrWhiteSpace(parsed.City)) changes.Add($"city → {parsed.City.Trim()}");
                    if (!string.IsNullOrWhiteSpace(parsed.Category)) changes.Add($"category → {parsed.Category.Trim()}");
                    if (parsed.Description != null) changes.Add($"description → {parsed.Description.Trim()}");
                    if (!string.IsNullOrWhiteSpace(parsed.Status)) changes.Add($"status → {parsed.Status.Trim()}");
                    if (parsed.Tags != null) changes.Add($"tags → {parsed.Tags.Trim()}");
                    if (parsed.EgyptianPrice.HasValue) changes.Add($"Egyptian price → {parsed.EgyptianPrice} EGP");
                    if (parsed.StudentEgyptianPrice.HasValue) changes.Add($"Egyptian student price → {parsed.StudentEgyptianPrice} EGP");
                    if (parsed.ForeignPrice.HasValue) changes.Add($"foreign price → {parsed.ForeignPrice} EGP");
                    if (parsed.StudentForeignPrice.HasValue) changes.Add($"foreign student price → {parsed.StudentForeignPrice} EGP");
                    if (parsed.OpenAt.HasValue) changes.Add($"opens at → {parsed.OpenAt}:00");
                    if (parsed.CloseAt.HasValue) changes.Add($"closes at → {parsed.CloseAt}:00");
                    if (!changes.Any())
                        return Fail("I couldn't see any changes to apply. What would you like to update?");
                    return Ok($"I'm ready to update **{existing.Name}**:\n- {string.Join("\n- ", changes)}");
                }

                var candidate = new Destination
                {
                    Id = existing.Id,
                    Name = string.IsNullOrWhiteSpace(parsed.Name) ? existing.Name : parsed.Name.Trim(),
                    ArabicName = parsed.ArabicName != null ? (string.IsNullOrWhiteSpace(parsed.ArabicName) ? null : parsed.ArabicName.Trim()) : existing.ArabicName,
                    City = string.IsNullOrWhiteSpace(parsed.City) ? existing.City : parsed.City.Trim(),
                    Category = string.IsNullOrWhiteSpace(parsed.Category) ? existing.Category : parsed.Category.Trim(),
                    Description = parsed.Description != null ? (string.IsNullOrWhiteSpace(parsed.Description) ? null : parsed.Description.Trim()) : existing.Description,
                    Status = string.IsNullOrWhiteSpace(parsed.Status) ? existing.Status : parsed.Status.Trim(),
                    Tags = parsed.Tags != null ? (string.IsNullOrWhiteSpace(parsed.Tags) ? null : parsed.Tags.Trim()) : existing.Tags,
                    EgyptianPrice = parsed.EgyptianPrice.HasValue ? parsed.EgyptianPrice : existing.EgyptianPrice,
                    StudentEgyptianPrice = parsed.StudentEgyptianPrice.HasValue ? parsed.StudentEgyptianPrice : existing.StudentEgyptianPrice,
                    ForeignPrice = parsed.ForeignPrice.HasValue ? parsed.ForeignPrice : existing.ForeignPrice,
                    StudentForeignPrice = parsed.StudentForeignPrice.HasValue ? parsed.StudentForeignPrice : existing.StudentForeignPrice,
                    OpenAt = parsed.OpenAt.HasValue ? parsed.OpenAt : existing.OpenAt,
                    CloseAt = parsed.CloseAt.HasValue ? parsed.CloseAt : existing.CloseAt,
                    Location = existing.Location,
                    PhotoUrls = existing.PhotoUrls,
                    TicketPrice = existing.TicketPrice,
                    TicketRequired = existing.TicketRequired,
                    Days = existing.Days,
                    Booking = existing.Booking,
                    Visits = existing.Visits,
                    Rating = existing.Rating
                };

                var syncResult = await _arcgisSync.UpdateDestinationOnArcGISAsync(candidate, ct);
                if (!syncResult.Success)
                    return Fail($"I couldn't update the destination because the map service is unavailable ({syncResult.Error}). Nothing was changed.");

                try
                {
                    existing.Name = candidate.Name;
                    existing.ArabicName = candidate.ArabicName;
                    existing.City = candidate.City;
                    existing.Category = candidate.Category;
                    existing.Description = candidate.Description;
                    existing.Status = candidate.Status;
                    existing.Tags = candidate.Tags;
                    existing.EgyptianPrice = candidate.EgyptianPrice;
                    existing.StudentEgyptianPrice = candidate.StudentEgyptianPrice;
                    existing.ForeignPrice = candidate.ForeignPrice;
                    existing.StudentForeignPrice = candidate.StudentForeignPrice;
                    existing.OpenAt = candidate.OpenAt;
                    existing.CloseAt = candidate.CloseAt;
                    _destinationRepo.Update(existing);
                    _destinationRepo.Save();
                }
                catch (Exception)
                {
                    return Fail("The map service was updated, but the local copy couldn't be synchronized. Please ask an administrator to reconcile it.");
                }

                return Ok($"Done! The destination **{candidate.Name}** has been updated successfully.");
            }
        };

        // ============================================================
        // delete_destination (ArcGIS, mirrors DestinationController.DeleteConfirmed)
        // ============================================================
        private AiToolDefinition DeleteDestination() => new()
        {
            Name = "delete_destination",
            Description = "Delete an existing destination (destination_id). Removes the destination from the map service and the local database, " +
                          "along with its missions and trip stops. Destructive — the system always asks for explicit confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { destination_id = new { type = "INTEGER", description = "The destination's ID." } },
                required = new[] { "destination_id" }
            },
            Roles = AdminRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var parsed = AiToolsCommon.ParseArgs<DestinationIdArgs>(args);
                if (parsed == null || parsed.DestinationId <= 0)
                    return Fail("I need a valid destination to delete.");

                var destination = _destinationRepo.GetById(parsed.DestinationId);
                if (destination == null)
                    return Fail("I couldn't find that destination.");

                if (context.IsPreview)
                {
                    return Ok(
                        $"I found the destination **{destination.Name}** in {destination.City}. Deleting it will also remove its missions and trip stops. " +
                        "Are you sure you want to delete it?");
                }

                // ArcGIS is authoritative: do not touch local rows until the remote feature is confirmed deleted.
                var arcgisResult = await _arcgisSync.DeleteDestinationFromArcGISAsync(parsed.DestinationId, ct);
                if (!arcgisResult.Success)
                    return Fail($"The destination was not deleted because the map service could not confirm the removal ({arcgisResult.Error}).");

                try
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(ct);
                    var missions = _context.Missions.Where(m => m.DestinationId == parsed.DestinationId).ToList();
                    var missionIds = missions.Select(m => m.Id).ToList();
                    if (missionIds.Count > 0)
                    {
                        _context.UserMissions.RemoveRange(_context.UserMissions.Where(um => missionIds.Contains(um.MissionId)));
                        _context.Missions.RemoveRange(missions);
                    }
                    _context.TripDestinations.RemoveRange(_context.TripDestinations.Where(td => td.DestinationId == parsed.DestinationId));
                    _context.SiteReviews.Where(r => r.DestinationId == parsed.DestinationId).ToList().ForEach(r => r.DestinationId = null);
                    _context.Favorites.RemoveRange(_context.Favorites.Where(f => f.ItemType == FavoriteItemType.Destination && f.ItemId == parsed.DestinationId));
                    _destinationRepo.Delete(parsed.DestinationId);
                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch (Exception)
                {
                    return Fail("The map service removal succeeded, but the local database couldn't be synchronized. Please ask an administrator to reconcile it.");
                }

                return Ok($"Done! The destination **{destination.Name}** has been deleted.");
            }
        };

        // ============================================================
        // Helpers
        // ============================================================
        private sealed record AccountSummaryRow(string Email, string Name, string Role);

        private static Destination BuildDestinationFromDraft(AdminDestinationDraftArgs parsed, double lat, double lon, List<string> imageUrls, bool isPublic)
        {
            var destination = new Destination
            {
                Name = parsed.Name.Trim(),
                ArabicName = string.IsNullOrWhiteSpace(parsed.ArabicName) ? null : parsed.ArabicName.Trim(),
                City = parsed.City.Trim(),
                Category = parsed.Category.Trim(),
                Description = string.IsNullOrWhiteSpace(parsed.Description) ? null : parsed.Description.Trim(),
                Tags = string.IsNullOrWhiteSpace(parsed.Tags) ? null : parsed.Tags.Trim(),
                Location = new Point(lon, lat) { SRID = 4326 },
                PhotoUrls = imageUrls.Any() ? string.Join("\n", imageUrls) : null,
                Status = "Active",
                Visits = 0,
                Rating = 0m
            };

            if (isPublic)
            {
                // Public destinations: free-form access, all-day schedule.
                destination.TicketRequired = "No";
                destination.TicketPrice = null;
                destination.EgyptianPrice = null;
                destination.StudentEgyptianPrice = null;
                destination.ForeignPrice = null;
                destination.StudentForeignPrice = null;
                destination.Booking = null;
                destination.Days = "All Days";
                destination.OpenAt = 0;
                destination.CloseAt = 23;
            }
            else
            {
                destination.TicketRequired = parsed.TicketRequired;
                destination.EgyptianPrice = parsed.EgyptianPrice;
                destination.StudentEgyptianPrice = parsed.StudentEgyptianPrice;
                destination.ForeignPrice = parsed.ForeignPrice;
                destination.StudentForeignPrice = parsed.StudentForeignPrice;
                destination.TicketPrice = parsed.EgyptianPrice ?? parsed.ForeignPrice;
            }

            return destination;
        }

        private static AiToolResult Ok(string message, object? data = null) =>
            new() { Success = true, Message = message, Data = data };

        private static AiToolResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
