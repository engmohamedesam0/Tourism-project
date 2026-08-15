using System.Text.Json;
using NetTopologySuite.Geometries;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiTools
{
    /// <summary>
    /// Sponsor tools. Every branch/reward is bound to the Sponsor resolved
    /// server-side from the authenticated identity — a Sponsor ID supplied by the
    /// user or the model is NEVER used. Ownership is re-checked at write time.
    /// </summary>
    public class SponsorAiTools
    {
        private static readonly string[] SponsorRole = { AiIdentityContext.RoleSponsor };

        private readonly IBranchRepository _branchRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly TouristContext _context;
        private readonly IArcGISSyncService _arcgisSync;

        public SponsorAiTools(
            IBranchRepository branchRepo,
            IRewardRepository rewardRepo,
            TouristContext context,
            IArcGISSyncService arcgisSync)
        {
            _branchRepo = branchRepo;
            _rewardRepo = rewardRepo;
            _context = context;
            _arcgisSync = arcgisSync;
        }

        public List<AiToolDefinition> Build() => new()
        {
            CreateBranches(),
            GetMyBranches(),
            UpdateBranch(),
            DeleteBranch(),
            GetMyRewards(),
            CreateReward(),
            UpdateReward(),
            DeleteReward(),
            GetMyProfile()
        };

        // ============================================================
        // create_branch
        // ============================================================
        private AiToolDefinition CreateBranches() => new()
        {
            Name = "create_branch",
            Description = "Create one or more branches for the signed-in sponsor. Each branch needs: name (required), address (required), and a location " +
                          "(either latitude/longitude or a recognizable Egyptian city name such as Cairo, Giza, Alexandria, Luxor...). " +
                          "contact_number is optional. IMPORTANT: branches have NO price field in this system — prices are managed through rewards. " +
                          "If any required field is missing, ask the user BEFORE calling. The system will ask the user to confirm before creating.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    branches = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                name = new { type = "STRING", description = "Branch name (e.g. \"Cairo Branch\")." },
                                address = new { type = "STRING", description = "Street address of the branch." },
                                city = new { type = "STRING", description = "Optional Egyptian city for location lookup (e.g. Cairo, Giza)." },
                                latitude = new { type = "NUMBER", description = "Optional latitude (use instead of city when known)." },
                                longitude = new { type = "NUMBER", description = "Optional longitude (use instead of city when known)." },
                                contact_number = new { type = "INTEGER", description = "Optional phone number." }
                            },
                            required = new[] { "name", "address" }
                        },
                        description = "The branches to create."
                    }
                },
                required = new[] { "branches" }
            },
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Fail("You'll need to sign in as a sponsor first.");

                var parsed = AiToolsCommon.ParseArgs<CreateBranchesArgs>(args);
                if (parsed == null || !parsed.Branches.Any())
                    return Fail("I couldn't understand the branch details. Could you tell me the branch name and address again?");

                var resolved = new List<(BranchDraftArgs Draft, double Lat, double Lon)>();
                foreach (var draft in parsed.Branches)
                {
                    if (string.IsNullOrWhiteSpace(draft.Name) || string.IsNullOrWhiteSpace(draft.Address))
                        return Fail("Each branch needs a name and an address.");

                    if (!AiToolsCommon.TryResolveLocation(draft.Latitude, draft.Longitude, draft.City, out var lat, out var lon))
                    {
                        return Fail($"I need a location for \"{draft.Name.Trim()}\" — could you give me a recognizable Egyptian city or the latitude/longitude?");
                    }
                    resolved.Add((draft, lat, lon));
                }

                if (context.IsPreview)
                {
                    var lines = resolved.Select(r =>
                        $"- **{r.Draft.Name.Trim()}** — {r.Draft.Address.Trim()}" +
                        (string.IsNullOrWhiteSpace(r.Draft.City) ? $" ({r.Lat:0.####}, {r.Lon:0.####})" : $" ({r.Draft.City.Trim()})") +
                        (r.Draft.ContactNumber.HasValue ? $", phone {r.Draft.ContactNumber}" : ""));
                    return Ok($"I'm ready to create {resolved.Count} branch{(resolved.Count == 1 ? "" : "es")}:\n" +
                              string.Join("\n", lines) +
                              "\n\n(Note: branches don't store prices — prices are managed through rewards.)");
                }

                var created = new List<Branch>();
                foreach (var (draft, lat, lon) in resolved)
                {
                    var branch = new Branch
                    {
                        Name = draft.Name.Trim(),
                        Address = draft.Address.Trim(),
                        Category = sponsor.Type, // branch category always follows its sponsor
                        Location = new Point(lon, lat) { SRID = 4326 },
                        ContactNumber = draft.ContactNumber,
                        SponsorId = sponsor.Id
                    };
                    _branchRepo.Add(branch);
                    created.Add(branch);
                }
                _branchRepo.Save();

                var arcgisMessage = "";
                try
                {
                    var result = await _arcgisSync.SyncBranchesAsync(created);
                    if (!result.Success)
                        arcgisMessage = $" (Note: the map sync failed: {result.Error})";
                }
                catch (Exception)
                {
                    arcgisMessage = " (Note: the map sync is currently unavailable.)";
                }

                var reply = $"Done! {created.Count} branch{(created.Count == 1 ? "" : "es")} created successfully: " +
                            $"{string.Join(", ", created.Select(b => b.Name))}.{arcgisMessage}";
                return Ok(reply);
            }
        };

        // ============================================================
        // get_my_branches
        // ============================================================
        private AiToolDefinition GetMyBranches() => new()
        {
            Name = "get_my_branches",
            Description = "List the signed-in sponsor's OWN branches.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = SponsorRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var branches = _branchRepo.GetBySponsorId(sponsor.Id).ToList();
                var items = branches.Select(b => new
                {
                    branch_id = b.Id,
                    name = b.Name,
                    address = b.Address,
                    latitude = b.Location?.Y,
                    longitude = b.Location?.X,
                    contact_number = b.ContactNumber
                }).ToList();

                var message = branches.Count == 0
                    ? "You don't have any branches yet. I'd be happy to help you create one!"
                    : $"You have {branches.Count} branch{(branches.Count == 1 ? "" : "es")}:\n" +
                      string.Join("\n", branches.Select(b =>
                          $"- **{b.Name}** (id={b.Id}) — {b.Address}" +
                          (b.ContactNumber.HasValue ? $", phone {b.ContactNumber}" : "")));

                return Task.FromResult(Ok(message, new { branches = items }));
            }
        };

        // ============================================================
        // update_branch
        // ============================================================
        private AiToolDefinition UpdateBranch() => new()
        {
            Name = "update_branch",
            Description = "Update one of the signed-in sponsor's OWN branches (branch_id from get_my_branches). " +
                          "Only include fields that change. The system will ask for confirmation before saving.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    branch_id = new { type = "INTEGER", description = "The branch's ID." },
                    name = new { type = "STRING", description = "Optional new name." },
                    address = new { type = "STRING", description = "Optional new address." },
                    city = new { type = "STRING", description = "Optional Egyptian city to move the location to." },
                    latitude = new { type = "NUMBER", description = "Optional new latitude." },
                    longitude = new { type = "NUMBER", description = "Optional new longitude." },
                    contact_number = new { type = "INTEGER", description = "Optional new phone number." }
                },
                required = new[] { "branch_id" }
            },
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = async (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Fail("You'll need to sign in as a sponsor first.");

                var parsed = AiToolsCommon.ParseArgs<UpdateBranchArgs>(args);
                if (parsed == null || parsed.BranchId <= 0)
                    return Fail("I need a valid branch to update.");

                var branch = _branchRepo.GetById(parsed.BranchId);
                if (branch == null || branch.SponsorId != sponsor.Id)
                    return Fail("I couldn't find that branch on your account.");

                if (context.IsPreview)
                {
                    var changes = new List<string>();
                    if (!string.IsNullOrWhiteSpace(parsed.Name)) changes.Add($"name → {parsed.Name.Trim()}");
                    if (!string.IsNullOrWhiteSpace(parsed.Address)) changes.Add($"address → {parsed.Address.Trim()}");
                    if (parsed.Latitude.HasValue && parsed.Longitude.HasValue) changes.Add($"location → ({parsed.Latitude.Value:0.####}, {parsed.Longitude.Value:0.####})");
                    else if (!string.IsNullOrWhiteSpace(parsed.City)) changes.Add($"location → {parsed.City.Trim()}");
                    if (parsed.ContactNumber.HasValue) changes.Add($"phone → {parsed.ContactNumber}");
                    if (!changes.Any())
                        return Fail("I couldn't see any changes to apply. What would you like to update?");
                    return Ok($"I'm ready to update **{branch.Name}**:\n- {string.Join("\n- ", changes)}");
                }

                if (!string.IsNullOrWhiteSpace(parsed.Name)) branch.Name = parsed.Name.Trim();
                if (!string.IsNullOrWhiteSpace(parsed.Address)) branch.Address = parsed.Address.Trim();
                if (parsed.Latitude.HasValue && parsed.Longitude.HasValue)
                {
                    branch.Location = new Point(parsed.Longitude.Value, parsed.Latitude.Value) { SRID = 4326 };
                }
                else if (!string.IsNullOrWhiteSpace(parsed.City) &&
                         AiToolsCommon.TryResolveLocation(null, null, parsed.City, out var lat, out var lon))
                {
                    branch.Location = new Point(lon, lat) { SRID = 4326 };
                }
                if (parsed.ContactNumber.HasValue) branch.ContactNumber = parsed.ContactNumber;

                _branchRepo.Update(branch);
                _branchRepo.Save();
                try
                {
                    await _arcgisSync.SyncBranchesAsync(new[] { branch });
                }
                catch (Exception)
                {
                    // map sync failure is non-fatal for the DB update
                }

                return Ok($"Done! Your branch **{branch.Name}** has been updated successfully.");
            }
        };

        // ============================================================
        // delete_branch
        // ============================================================
        private AiToolDefinition DeleteBranch() => new()
        {
            Name = "delete_branch",
            Description = "Delete one of the signed-in sponsor's OWN branches (branch_id from get_my_branches). " +
                          "Destructive — the system always asks for explicit confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { branch_id = new { type = "INTEGER", description = "The branch's ID." } },
                required = new[] { "branch_id" }
            },
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var parsed = AiToolsCommon.ParseArgs<BranchIdArgs>(args);
                if (parsed == null || parsed.BranchId <= 0)
                    return Task.FromResult(Fail("I need a valid branch to delete."));

                var branch = _branchRepo.GetById(parsed.BranchId);
                if (branch == null || branch.SponsorId != sponsor.Id)
                    return Task.FromResult(Fail("I couldn't find that branch on your account."));

                if (context.IsPreview)
                {
                    return Task.FromResult(Ok(
                        $"I found your branch **{branch.Name}** at {branch.Address}. Are you sure you want to delete it?"));
                }

                _branchRepo.Delete(branch.Id);
                _branchRepo.Save();
                return Task.FromResult(Ok($"Done! Your branch **{branch.Name}** has been deleted."));
            }
        };

        // ============================================================
        // get_my_rewards
        // ============================================================
        private AiToolDefinition GetMyRewards() => new()
        {
            Name = "get_my_rewards",
            Description = "List the signed-in sponsor's OWN rewards (offers) with points, status and expiration.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = SponsorRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var rewards = _rewardRepo.GetBySponsorId(sponsor.Id).ToList();
                var items = rewards.Select(r => new
                {
                    reward_id = r.Id,
                    title = r.Title,
                    reward_type = r.RewardType,
                    description = r.Description,
                    points_required = r.PointsRequired,
                    quantity_available = r.QuantityAvailable,
                    status = r.Status,
                    expires = r.ExpirationDate.ToString("yyyy-MM-dd"),
                    branches = r.RewardBranches?.Select(rb => rb.Branch?.Name).ToList() ?? new List<string?>()
                }).ToList();

                var message = rewards.Count == 0
                    ? "You don't have any rewards yet. I'd be happy to help you create one!"
                    : $"You have {rewards.Count} reward{(rewards.Count == 1 ? "" : "s")}:\n" +
                      string.Join("\n", rewards.Select(r =>
                          $"- **{r.Title}** (id={r.Id}, {r.RewardType}) — {r.PointsRequired} points, status {r.Status}, expires {r.ExpirationDate:MMM d, yyyy}"));

                return Task.FromResult(Ok(message, new { rewards = items }));
            }
        };

        // ============================================================
        // create_reward
        // ============================================================
        private AiToolDefinition CreateReward() => new()
        {
            Name = "create_reward",
            Description = "Create a new reward/offer for the signed-in sponsor. Required: title, reward_type (e.g. Discount, Voucher, Gift), " +
                          "points_required (>= 1), expiration_date (YYYY-MM-DD — the reward's end date; there is no separate start-date field). " +
                          "quantity_available defaults to 0, status defaults to Active. branch_ids are optional and must be the sponsor's own branches. " +
                          "If any required field is missing, ask the user BEFORE calling. The system will ask for confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING", description = "Reward title (e.g. \"Summer Explorer\")." },
                    reward_type = new { type = "STRING", description = "Reward type (e.g. Discount, Voucher, Gift, Experience)." },
                    description = new { type = "STRING", description = "Optional description." },
                    points_required = new { type = "INTEGER", description = "Points required to redeem (at least 1)." },
                    quantity_available = new { type = "INTEGER", description = "Optional quantity available (default 0)." },
                    expiration_date = new { type = "STRING", description = "Expiration/end date (YYYY-MM-DD)." },
                    status = new { type = "STRING", description = "Optional status (Active, Paused, Removed). Default Active." },
                    branch_ids = new
                    {
                        type = "ARRAY",
                        items = new { type = "INTEGER" },
                        description = "Optional branches where the reward is available (the sponsor's own branch IDs)."
                    }
                },
                required = new[] { "title", "reward_type", "points_required", "expiration_date" }
            },
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var parsed = AiToolsCommon.ParseArgs<SponsorRewardDraftArgs>(args);
                if (parsed == null)
                    return Task.FromResult(Fail("I couldn't understand the reward details. Could you tell me again?"));

                if (string.IsNullOrWhiteSpace(parsed.Title) || string.IsNullOrWhiteSpace(parsed.RewardType))
                    return Task.FromResult(Fail("The reward needs a title and a type (e.g. Discount, Voucher)."));
                if (parsed.PointsRequired < 1)
                    return Task.FromResult(Fail("Points required must be at least 1."));

                var expiration = AiToolsCommon.ParseDateOrDefault(parsed.ExpirationDate, DateTime.Today.AddMonths(1));
                var status = string.IsNullOrWhiteSpace(parsed.Status) ? "Active" : parsed.Status.Trim();
                var quantity = parsed.QuantityAvailable ?? 0;

                // Branch IDs must belong to this sponsor — never trust foreign IDs.
                var ownBranchIds = _branchRepo.GetBySponsorId(sponsor.Id).Select(b => b.Id).ToHashSet();
                var validBranchIds = (parsed.BranchIds ?? new List<int>()).Where(ownBranchIds.Contains).Distinct().ToList();

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
                        (validBranchIds.Any()
                            ? $"Branches: {string.Join(", ", _branchRepo.GetBySponsorId(sponsor.Id).Where(b => validBranchIds.Contains(b.Id)).Select(b => b.Name))}\n"
                            : "");
                    return Task.FromResult(Ok(summary.TrimEnd()));
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

                SyncRewardBranches(reward.Id, validBranchIds);

                var reply = $"Done! Your reward **{parsed.Title.Trim()}** has been created successfully " +
                            $"({parsed.PointsRequired} points, expires {AiToolsCommon.ShortDate(expiration)}).";
                return Task.FromResult(Ok(reply));
            }
        };

        // ============================================================
        // update_reward
        // ============================================================
        private AiToolDefinition UpdateReward() => new()
        {
            Name = "update_reward",
            Description = "Update one of the signed-in sponsor's OWN rewards (reward_id from get_my_rewards). " +
                          "Only include fields that change. The system will ask for confirmation before saving.",
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
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var parsed = AiToolsCommon.ParseArgs<UpdateRewardArgs>(args);
                if (parsed == null || parsed.RewardId <= 0)
                    return Task.FromResult(Fail("I need a valid reward to update."));

                var reward = _context.Rewards.FirstOrDefault(r => r.Id == parsed.RewardId);
                if (reward == null || reward.SponsorId != sponsor.Id)
                    return Task.FromResult(Fail("I couldn't find that reward on your account."));

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
                return Task.FromResult(Ok($"Done! Your reward **{reward.Title}** has been updated successfully."));
            }
        };

        // ============================================================
        // delete_reward
        // ============================================================
        private AiToolDefinition DeleteReward() => new()
        {
            Name = "delete_reward",
            Description = "Remove one of the signed-in sponsor's OWN rewards (reward_id from get_my_rewards). " +
                          "Destructive — the system always asks for explicit confirmation first.",
            Parameters = new
            {
                type = "OBJECT",
                properties = new { reward_id = new { type = "INTEGER", description = "The reward's ID." } },
                required = new[] { "reward_id" }
            },
            Roles = SponsorRole,
            RequiresConfirmation = true,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var parsed = AiToolsCommon.ParseArgs<RewardIdArgs>(args);
                if (parsed == null || parsed.RewardId <= 0)
                    return Task.FromResult(Fail("I need a valid reward to remove."));

                var reward = _context.Rewards.FirstOrDefault(r => r.Id == parsed.RewardId);
                if (reward == null || reward.SponsorId != sponsor.Id)
                    return Task.FromResult(Fail("I couldn't find that reward on your account."));

                if (context.IsPreview)
                {
                    return Task.FromResult(Ok(
                        $"I found your reward **{reward.Title}** ({reward.PointsRequired} points, expires {AiToolsCommon.ShortDate(reward.ExpirationDate)}). " +
                        "Are you sure you want to remove it?"));
                }

                // Mirrors SponsorRewardController.DeleteConfirmed: soft removal.
                reward.Status = "Removed";
                _rewardRepo.Update(reward);
                _rewardRepo.Save();
                return Task.FromResult(Ok($"Done! Your reward **{reward.Title}** has been removed."));
            }
        };

        // ============================================================
        // get_my_profile
        // ============================================================
        private AiToolDefinition GetMyProfile() => new()
        {
            Name = "get_my_profile",
            Description = "Show the signed-in sponsor's profile: name, type, address, contact info, and counts of their branches and rewards.",
            Parameters = new { type = "OBJECT", properties = new { } },
            Roles = SponsorRole,
            ExecuteAsync = (args, context, ct) =>
            {
                var sponsor = context.Identity.Sponsor;
                if (sponsor == null)
                    return Task.FromResult(Fail("You'll need to sign in as a sponsor first."));

                var branchCount = _branchRepo.GetBySponsorId(sponsor.Id).Count();
                var rewardCount = _rewardRepo.GetBySponsorId(sponsor.Id).Count();

                var profile = new
                {
                    name = sponsor.Name,
                    type = sponsor.Type,
                    address = sponsor.Address,
                    contact_number = sponsor.ContactNumber,
                    email = sponsor.Email,
                    branches = branchCount,
                    rewards = rewardCount
                };

                var message =
                    $"Here's your sponsor profile:\n" +
                    $"- Name: {sponsor.Name}\n" +
                    (string.IsNullOrWhiteSpace(sponsor.Type) ? "" : $"- Type: {sponsor.Type}\n") +
                    $"- Address: {sponsor.Address}\n" +
                    $"- Contact number: {sponsor.ContactNumber}\n" +
                    (string.IsNullOrWhiteSpace(sponsor.Email) ? "" : $"- Email: {sponsor.Email}\n") +
                    $"- Branches: {branchCount}\n" +
                    $"- Rewards: {rewardCount}";

                return Task.FromResult(Ok(message.TrimEnd(), new { profile }));
            }
        };

        // ============================================================
        // Helpers
        // ============================================================
        private void SyncRewardBranches(int rewardId, List<int> branchIds)
        {
            var existing = _context.RewardBranches.Where(rb => rb.RewardId == rewardId).ToList();
            _context.RewardBranches.RemoveRange(existing);
            foreach (var branchId in branchIds)
            {
                _context.RewardBranches.Add(new RewardBranch { RewardId = rewardId, BranchId = branchId });
            }
            _context.SaveChanges();
        }

        private static AiToolResult Ok(string message, object? data = null) =>
            new() { Success = true, Message = message, Data = data };

        private static AiToolResult Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
