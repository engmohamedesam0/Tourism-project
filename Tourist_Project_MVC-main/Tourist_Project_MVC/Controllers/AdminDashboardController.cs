using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminDashboard")]
    public class AdminDashboardController : Controller
    {
        private readonly TouristContext _context;
        private readonly IArcGISSyncService _arcgisSync;
        private static readonly string[] Sections = new[]
        {
            "overview", "tourists", "missions", "sponsors",
            "destinations", "rewards", "levels", "badges", "support", "reviews"
        };

        public AdminDashboardController(TouristContext context, IArcGISSyncService arcgisSync)
        {
            _context = context;
            _arcgisSync = arcgisSync;
        }

        [HttpGet("")]
        [HttpGet("{section}")]
        public IActionResult Index(string? section, string? tab)
        {
            var normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
            if (!Sections.Contains(normalized))
            {
                var cookie = Request.Cookies["AdminDashboard.LastSection"];
                if (!string.IsNullOrEmpty(cookie) && Sections.Contains(cookie.ToLowerInvariant()))
                {
                    normalized = cookie.ToLowerInvariant();
                }
                else
                {
                    normalized = "overview";
                }
            }

            var vm = new AdminDashboardVM { ActiveSection = normalized };

            if (normalized == "overview")
            {
                BuildOverview(vm);
            }
            else
            {
                BuildOverview(vm);
                BuildSection(vm, normalized);
            }

            Response.Cookies.Append("AdminDashboard.LastSection", normalized, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return View("Index", vm);
        }

        private void BuildOverview(AdminDashboardVM vm)
        {
            var year = DateTime.Now.Year;

            var totalTourists = _context.Tourists.Count();
            var totalSponsors = _context.Sponsors.Count();
            var totalDestinations = _context.Destinations.Count();
            var totalBranches = _context.Branches.Count();
            var totalRewards = _context.Rewards.Count();
            var totalRedemptions = _context.Redemptions.Count();
            var totalMissionsCompleted = _context.UserMissions.Count();

            var siteReviews = _context.SiteReviews.ToList();
            var ratingAvailable = siteReviews.Any();
            var averageRating = ratingAvailable ? siteReviews.Average(r => r.Rating) : (double?)null;
            var reviewCount = siteReviews.Count;

            var redemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Where(r => r.Reward != null && r.RedemptionDate.Year == year)
                .ToList();

            var rewardViews = _context.RewardViews
                .Include(v => v.Reward)
                .Where(v => v.Reward != null && v.ViewedDate.Year == year)
                .ToList();

            var redemptionGroups = redemptions
                .GroupBy(r => r.RedemptionDate.Month)
                .ToDictionary(g => g.Key, g => new
                {
                    Count = g.Count(),
                    Points = g.Sum(r => r.PointsRedeemed)
                });

            var viewGroups = rewardViews
                .GroupBy(v => v.ViewedDate.Month)
                .ToDictionary(g => g.Key, g => g.Count());

            var monthlyStats = Enumerable.Range(1, 12)
                .Select(month => new MonthlyStatRow
                {
                    Year = year,
                    Month = month,
                    MonthLabel = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month),
                    Redemptions = redemptionGroups.TryGetValue(month, out var rg) ? rg.Count : 0,
                    PointsRedeemed = redemptionGroups.TryGetValue(month, out var rg2) ? rg2.Points : 0,
                    Views = viewGroups.TryGetValue(month, out var vg) ? vg : 0
                })
                .ToList();

            var dashboardTopRewards = redemptions
                .GroupBy(r => r.Reward!.Id)
                .Select(g => new TopRewardRow
                {
                    RewardTitle = g.First().Reward!.Title,
                    Redemptions = g.Count(),
                    Views = rewardViews.Count(v => v.RewardId == g.Key)
                })
                .OrderByDescending(t => t.Redemptions)
                .ToList();

            var allBranches = _context.Branches
                .Where(b => b.Location != null)
                .Select(b => new BranchMapPoint
                {
                    Id = b.Id,
                    Name = b.Name,
                    Lat = b.Location.Y,
                    Lng = b.Location.X
                })
                .ToList();

            vm.TotalTourists = totalTourists;
            vm.TotalSponsors = totalSponsors;
            vm.TotalDestinations = totalDestinations;
            vm.TotalBranches = totalBranches;
            vm.TotalRewards = totalRewards;
            vm.TotalRedemptions = totalRedemptions;
            vm.TotalMissionsCompleted = totalMissionsCompleted;
            vm.RatingAvailable = ratingAvailable;
            vm.AverageRating = averageRating;
            vm.ReviewCount = reviewCount;
            vm.MonthlyStats = monthlyStats;
            vm.DashboardTopRewards = dashboardTopRewards;
            vm.AllBranches = allBranches;
        }

        private void BuildSection(AdminDashboardVM vm, string section)
        {
            switch (section)
            {
                case "tourists":
                    BuildTouristSection(vm);
                    break;
                case "missions":
                    BuildMissionSection(vm);
                    break;
                case "sponsors":
                    BuildSponsorSection(vm);
                    break;
                case "destinations":
                    BuildDestinationSection(vm);
                    break;
                case "rewards":
                    BuildRewardSection(vm);
                    break;
                case "levels":
                    BuildLevelSection(vm);
                    break;
                case "badges":
                    BuildBadgeSection(vm);
                    break;
                case "support":
                    BuildSupportSection(vm);
                    break;
                case "reviews":
                    BuildReviewSection(vm);
                    break;
            }
        }

        private void BuildTouristSection(AdminDashboardVM vm)
        {
            var allTourists = _context.Tourists
                .Include(t => t.UserMissions)
                .Include(t => t.UserProgress)
                .Include(t => t.UserBadges)
                .Include(t => t.ApplicationUser)
                .ToList();
            var destinations = _context.Destinations.ToList();
            var trips = _context.TripPlans
                .Include(t => t.TripDestinations)
                .ThenInclude(td => td.Destination)
                .ToList();
            var tripStops = _context.TripDestinations
                .Include(td => td.Destination)
                .Include(td => td.TripPlan)
                .ToList();
            var reviews = _context.SiteReviews
                .Include(r => r.Destination)
                .ToList();
            var favorites = _context.Favorites.ToList();
            var now = DateTime.Now;

            vm.TouristSection.Total = allTourists.Count;
            var active = allTourists.Count(t => t.Status == "Active");
            var inactive = allTourists.Count(t => t.Status == "Inactive");
            var suspended = allTourists.Count(t => t.Status == "Suspended");
            vm.TouristSection.Active = active;
            vm.TouristSection.Inactive = inactive;
            vm.TouristSection.Suspended = suspended;
            vm.TouristSection.GrowthPercentage = allTourists.Count == 0 ? 0 : Math.Round(allTourists.Count(t => t.RegisterDate >= now.AddDays(-30)) * 100d / allTourists.Count, 1);
            vm.TouristSection.RetentionRate = allTourists.Count == 0 ? 0 : Math.Round(active * 100d / allTourists.Count, 1);
            vm.TouristSection.AverageSessionDuration = "Not tracked";
            vm.TouristSection.AverageMissionsCompleted = allTourists.Any() ? Math.Round(allTourists.Average(t => t.UserMissions?.Count ?? 0), 1) : 0;

            vm.TouristSection.NationalityBreakdown = allTourists
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Nationality) ? "Unknown" : t.Nationality)
                .Select(g => new AdminDashboardVM.NameCountRow(g.Key, "globe2", g.Count()))
                .OrderByDescending(r => r.Count).Take(8).ToList();
            vm.TouristSection.StatusBreakdown = new List<AdminDashboardVM.NameCountRow>
            {
                new("Active", "person-check", active), new("Inactive", "person-dash", inactive), new("Suspended", "person-x", suspended)
            };

            var months = Enumerable.Range(0, 6).Select(i => now.AddMonths(-5 + i)).ToList();
            vm.TouristSection.MonthsLabels = months.Select(m => m.ToString("MMM")).ToList();
            vm.TouristSection.ActiveHistory = months.Select(m => allTourists.Count(t => t.RegisterDate.Year == m.Year && t.RegisterDate.Month == m.Month)).ToList();
            vm.TouristSection.InactiveHistory = months.Select(m => allTourists.Count(t => t.Status == "Inactive" && t.RegisterDate.Year == m.Year && t.RegisterDate.Month == m.Month)).ToList();
            vm.TouristSection.SuspendedHistory = months.Select(m => allTourists.Count(t => t.Status == "Suspended" && t.RegisterDate.Year == m.Year && t.RegisterDate.Month == m.Month)).ToList();

            var visitorsByDestination = tripStops
                .Where(td => td.Destination != null)
                .GroupBy(td => td.DestinationId)
                .ToDictionary(g => g.Key, g => g.Count());
            var reviewsByDestination = reviews.Where(r => r.Destination != null).GroupBy(r => r.DestinationId!.Value).ToDictionary(g => g.Key, g => g.Average(r => r.Rating));
            var destinationRows = destinations.Select(d =>
            {
                var visitors = visitorsByDestination.TryGetValue(d.Id, out var count) ? count : d.Visits;
                var rating = reviewsByDestination.TryGetValue(d.Id, out var reviewRating) ? reviewRating : (double)(d.Rating ?? 0);
                var congestion = visitors >= 50 ? "High" : visitors >= 20 ? "Moderate" : "Low";
                var potential = rating >= 4 && visitors < 20 ? Math.Round(Math.Min(99, rating * 15 + (20 - visitors) * 2), 1) : 0;
                return new TourismDestinationRow(d.Name, d.City, visitors, Math.Round(rating, 1), 0, congestion, d.Status ?? "Active", false, potential);
            }).OrderByDescending(d => d.Visitors).ToList();
            vm.TouristSection.TopDestinations = destinationRows.Take(8).Select(d => new AdminDashboardVM.NameCountRow(d.Destination, "geo-alt", d.Visitors)).ToList();
            vm.MostVisitedDestination = destinationRows.FirstOrDefault()?.Destination ?? "No recorded visits";

            vm.TotalTrips = trips.Count;
            vm.RecordedDestinationVisits = tripStops.Count;
            vm.EngagedTourists = allTourists.Count(t => (t.UserMissions?.Any() ?? false) || (t.UserProgress?.VisitedPlaces ?? 0) > 0 || trips.Any(p => p.TouristId == t.Id));
            vm.EngagementRate = allTourists.Count == 0 ? 0 : Math.Round(vm.EngagedTourists * 100d / allTourists.Count, 1);
            vm.AverageStayDays = tripStops.Any() ? Math.Round(tripStops.Average(td => Math.Max(1, (td.DepartureDate.Date - td.ArrivalDate.Date).TotalDays)), 1) : 0;
            vm.HighPotentialDestinations = destinationRows.Count(d => d.Potential > 0);
            vm.DiscoveryPotential = destinationRows.Any() ? Math.Round(destinationRows.Where(d => d.Potential > 0).Select(d => d.Potential).DefaultIfEmpty(0).Average(), 1) : 0;
            vm.DestinationPerformance = destinationRows.Take(6).ToList();
            vm.HiddenDestinations = destinationRows.Where(d => d.Potential > 0).OrderByDescending(d => d.Potential).Take(5).Select(d => d with { IsHidden = true }).ToList();
            vm.DestinationOptions = destinations.Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();
            vm.RegionOptions = destinations.Select(d => d.City).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();

            vm.TourismFlows = tripStops.GroupBy(td => td.TripPlanId).SelectMany(g => g.OrderBy(td => td.Visit_Order).Zip(g.OrderBy(td => td.Visit_Order).Skip(1), (from, to) => new TourismFlowRow(from.Destination?.Name ?? "Unknown", to.Destination?.Name ?? "Unknown", 1)))
                .GroupBy(f => new { f.From, f.To }).Select(g => new TourismFlowRow(g.Key.From, g.Key.To, g.Count())).OrderByDescending(f => f.Volume).Take(6).ToList();
            vm.Congestion = destinationRows.Take(5).Select(d => new TourismCongestionRow(d.Destination, d.Congestion, d.Visitors, destinationRows.Sum(x => x.Visitors) == 0 ? 0 : Math.Round(d.Visitors * 100d / destinationRows.Sum(x => x.Visitors), 1))).ToList();

            // Avoid translating DateTime.Year/Month into PostgreSQL date_part casts.
            // On some PostgreSQL/Npgsql combinations that translation can overflow
            // when the provider converts date_part's numeric result back to int.
            // Restrict by a parameterized range in SQL, then group the materialized
            // dates in .NET where the values are already DateTime instances.
            var activityWindowStart = new DateTime(months[0].Year, months[0].Month, 1);
            var activityWindowEnd = months[^1].AddMonths(1);
            var missionActivityDates = _context.UserMissions
                .Where(um => um.Completed_At >= activityWindowStart && um.Completed_At < activityWindowEnd)
                .Select(um => um.Completed_At)
                .ToList();
            var missionsByMonth = missionActivityDates
                .GroupBy(date => new { date.Year, date.Month })
                .ToDictionary(group => (group.Key.Year, group.Key.Month), group => group.Count());
            vm.ActivityTrend = months.Select(m => new TourismSeriesPoint(
                m.ToString("MMM"),
                allTourists.Count(t => t.RegisterDate.Year == m.Year && t.RegisterDate.Month == m.Month),
                missionsByMonth.TryGetValue((m.Year, m.Month), out var missionCount) ? missionCount : 0)).ToList();

            var topNationality = vm.TouristSection.NationalityBreakdown.FirstOrDefault()?.Name ?? "No nationality data";
            vm.Insights = new List<TourismInsightRow>
            {
                new("Destination demand is concentrated", $"{vm.MostVisitedDestination} leads recorded destination visits. Use nearby low-traffic sites to balance the experience.", "graph-up-arrow", "gold"),
                new("Underexplored inventory detected", $"{vm.HighPotentialDestinations} destinations combine strong ratings with low recorded visits.", "stars", "blue"),
                new("Audience signal", $"{topNationality} is the largest nationality segment in the current tourist registry.", "globe2", "green")
            };
            vm.RecentTouristActivity = allTourists.OrderByDescending(t => t.RegisterDate).Take(4).Select(t => new TourismActivityRow("Tourist registered", $"{t.Name} joined the platform", RelativeTime(t.RegisterDate, now), "person-plus", "gold")).ToList();
            vm.RecentTouristActivity.AddRange(reviews.OrderByDescending(r => r.CreatedDate).Take(3).Select(r => new TourismActivityRow("Review submitted", $"{r.Destination?.Name ?? "A destination"} received a {r.Rating}/5 rating", RelativeTime(r.CreatedDate, now), "star", "blue")));

            vm.TouristSection.TopTouristsByPoints = allTourists.OrderByDescending(t => t.point_Balance).Take(5).Select(t => new TopTouristRow(t.Name, t.point_Balance, "star", "Points")).ToList();
            vm.TouristSection.TopTouristsByBadges = allTourists.OrderByDescending(t => t.UserBadges?.Count ?? 0).Take(5).Select(t => new TopTouristRow(t.Name, t.UserBadges?.Count ?? 0, "award", "Badges")).ToList();
            vm.TouristSection.TopTouristsByLevel = allTourists.OrderByDescending(t => t.UserProgress?.CurrentLevel ?? 0).Take(5).Select(t => new TopTouristRow(t.Name, t.UserProgress?.CurrentLevel ?? 0, "trophy", "Level")).ToList();
            vm.TouristSection.RecentActivities = vm.RecentTouristActivity.Select(a => new RecentActivityRow(a.Title, a.Detail, a.TimeAgo, $"bi-{a.Icon}", a.Tone)).ToList();
        }

        private static string RelativeTime(DateTime value, DateTime now)
        {
            var minutes = Math.Max(1, (int)(now - value).TotalMinutes);
            if (minutes < 60) return $"{minutes}m ago";
            if (minutes < 1440) return $"{minutes / 60}h ago";
            return $"{minutes / 1440}d ago";
        }

        private void BuildMissionSection(AdminDashboardVM vm)
        {
            vm.MissionSection.Total = _context.Missions.Count();
            vm.MissionSection.Completed = _context.UserMissions.Count(um => um.Status == "Completed");
            vm.MissionSection.Pending = _context.UserMissions.Count(um => um.Status != "Completed");

            var typeGroups = _context.Missions
                .GroupBy(m => m.MissionType)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "🎯", r.Count))
                .ToList();

            vm.MissionSection.TypeBreakdown = typeGroups;
        }

        private void BuildSponsorSection(AdminDashboardVM vm)
        {
            vm.SponsorSection.Total = _context.Sponsors.Count();

            var typeGroups = _context.Sponsors
                .GroupBy(s => s.Type)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "🏢", r.Count))
                .ToList();

            vm.SponsorSection.TypeBreakdown = typeGroups;
        }

        private void BuildDestinationSection(AdminDashboardVM vm)
        {
            vm.DestinationSection.Total = _context.Destinations.Count();
            vm.DestinationSection.Active = _context.Destinations.Count(d => d.Status == "Active");

            var categoryGroups = _context.Destinations
                .Where(d => !string.IsNullOrEmpty(d.Category))
                .GroupBy(d => d.Category!)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "📍", r.Count))
                .ToList();

            vm.DestinationSection.CategoryBreakdown = categoryGroups;
            vm.DestinationSection.Records = _context.Destinations
                .OrderByDescending(d => d.Visits)
                .Take(25)
                .Select(d => new DestinationAdminRow(d.Id, d.Name, d.City, d.Category, d.Status, d.Visits))
                .ToList();
        }

        private void BuildRewardSection(AdminDashboardVM vm)
        {
            vm.RewardSection.Total = _context.Rewards.Count();
            vm.RewardSection.Available = _context.Rewards.Count(r => r.Status == "Active");
            vm.RewardSection.TotalRedemptions = _context.Redemptions.Count();

            var typeGroups = _context.Rewards
                .GroupBy(r => r.RewardType)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "🎁", r.Count))
                .ToList();

            vm.RewardSection.TypeBreakdown = typeGroups;
        }

        private void BuildLevelSection(AdminDashboardVM vm)
        {
            var progresses = _context.UserProgress.ToList();
            vm.LevelSection.TouristsWithProgress = progresses.Count;

            var levelGroups = progresses
                .GroupBy(up => up.CurrentLevel)
                .Select(g =>
                {
                    var def = LevelDefinitions.GetLevelByNumber(g.Key);
                    return new AdminDashboardVM.NameCountRow($"{def.Icon} {def.Name}", def.Icon, g.Count());
                })
                .OrderBy(r => r.Count)
                .ToList();

            vm.LevelSection.LevelDistribution = levelGroups;
        }

        private void BuildBadgeSection(AdminDashboardVM vm)
        {
            vm.BadgeSection.TotalBadges = _context.Badges.Count();
            vm.BadgeSection.TotalEarned = _context.UserBadges.Count();

            var rarityGroups = _context.Badges
                .GroupBy(b => b.Rarity)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "🏅", r.Count))
                .ToList();

            vm.BadgeSection.RarityBreakdown = rarityGroups;
        }

        private void BuildSupportSection(AdminDashboardVM vm)
        {
            var tickets = _context.SupportTickets.ToList();
            vm.SupportSection.Total = tickets.Count;
            vm.SupportSection.Open = tickets.Count(t => t.Status == "Open");
            vm.SupportSection.Resolved = tickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");

            var statusGroups = tickets
                .GroupBy(t => t.Status)
                .Select(g => new AdminDashboardVM.NameCountRow(g.Key, "🎫", g.Count()))
                .OrderByDescending(r => r.Count)
                .ToList();

            vm.SupportSection.StatusBreakdown = statusGroups;
        }

        private void BuildReviewSection(AdminDashboardVM vm)
        {
            var reviews = _context.Reviews.ToList();
            vm.ReviewSection.Total = reviews.Count;
            vm.ReviewSection.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : (double?)null;

            var ratingGroups = Enumerable.Range(1, 5)
                .Select(star => new AdminDashboardVM.NameCountRow(
                    $"{star} ⭐",
                    "⭐",
                    reviews.Count(r => r.Rating == star)))
                .ToList();

            vm.ReviewSection.RatingDistribution = ratingGroups;
        }

        // -----------------------------------------------------------------------
        // ArcGIS On-Demand Sync Actions
        // -----------------------------------------------------------------------

        /// <summary>
        /// POST /AdminDashboard/SyncToArcGIS
        /// Pushes all local destinations (and branches) to the ArcGIS feature layers.
        /// Admins can trigger this manually whenever they add/update destinations locally.
        /// </summary>
        [HttpPost("SyncToArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncToArcGIS()
        {
            var destinations = await _context.Destinations
                .Where(d => d.Location != null)
                .ToListAsync();

            var branches = await _context.Branches
                .Where(b => b.Location != null)
                .ToListAsync();

            var destResult = await _arcgisSync.SyncDestinationsAsync(destinations);
            var branchResult = await _arcgisSync.SyncBranchesAsync(branches);

            if (destResult.Success && branchResult.Success)
            {
                TempData["ArcGISMessage"] = $"✅ Pushed to ArcGIS — {destResult.AddedCount} destinations added, " +
                    $"{destResult.UpdatedCount} updated; {branchResult.AddedCount} branches added, {branchResult.UpdatedCount} updated.";
                TempData["ArcGISMessageType"] = "success";
            }
            else
            {
                var errors = string.Join(" | ", new[] { destResult.Error, branchResult.Error }.Where(e => e != null));
                TempData["ArcGISMessage"] = $"❌ ArcGIS push failed: {errors}";
                TempData["ArcGISMessageType"] = "danger";
            }

            return RedirectToAction("Index", new { section = "destinations" });
        }

        /// <summary>
        /// POST /AdminDashboard/SyncFromArcGIS
        /// Pulls the latest destination data FROM ArcGIS into the local DB.
        /// </summary>
        [HttpPost("SyncFromArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncFromArcGIS()
        {
            var result = await _arcgisSync.SyncDestinationsFromArcGIS();

            if (result.Success)
            {
                TempData["ArcGISMessage"] = $"✅ Pulled from ArcGIS — {result.AddedCount} destinations synced into local DB.";
                TempData["ArcGISMessageType"] = "success";
            }
            else
            {
                TempData["ArcGISMessage"] = $"❌ ArcGIS pull failed: {result.Error}";
                TempData["ArcGISMessageType"] = "danger";
            }

            return RedirectToAction("Index", new { section = "destinations" });
        }

        // -----------------------------------------------------------------------
        // Admin -> Add Destination
        // -----------------------------------------------------------------------

        [HttpGet("Destinations/Add")]
        public IActionResult AddDestination()
        {
            var vm = new AddDestinationVM
            {
                Latitude = 0,
                Longitude = 0,
                LocationSelected = false
            };
            return View(vm);
        }

        [HttpPost("Destinations/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDestination(AddDestinationVM vm)
        {
            if (!vm.LocationSelected || (vm.Latitude == 0 && vm.Longitude == 0))
            {
                ModelState.AddModelError(string.Empty, "Please select the Destination location on the map.");
            }

            var isPublic = string.Equals(vm.Category?.Trim(), "Public", StringComparison.OrdinalIgnoreCase);
            if (isPublic)
            {
                // Public destinations are always free-form access: no booking or
                // ticket requirement and an all-day schedule are persisted so the
                // existing ArcGIS fields remain populated.
                vm.TicketRequired = "No";
                vm.EgyptianPrice = null;
                vm.StudentEgyptianPrice = null;
                vm.ForeignPrice = null;
                vm.StudentForeignPrice = null;
                vm.Booking = null;
                vm.SelectedDays = new List<string> { "All Days" };
                vm.OpenAt = 0;
                vm.CloseAt = 23;
            }

            var externalImageUrls = (vm.ExternalImageUrls ?? new List<string>())
                .Select(url => url?.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .Select(url => url!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (vm.ExternalImageUrls?.Any(url =>
                !string.IsNullOrWhiteSpace(url)
                && (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))) == true)
            {
                ModelState.AddModelError(nameof(vm.ExternalImageUrls), "Each external image URL must be a valid absolute URL.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // The layer has an Images URL field but no attachment support. Store
            // uploaded files under wwwroot so the URLs remain accessible to all
            // existing destination consumers after ArcGIS is created.
            var uploadedImageUrls = new List<string>();
            if (vm.ImageFiles != null && vm.ImageFiles.Count > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "destinations");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in vm.ImageFiles)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    if (!allowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError("ImageFiles", $"File '{file.FileName}' has an invalid format. Allowed: JPG, PNG, WEBP.");
                        return View(vm);
                    }
                    if (file.Length > 10 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFiles", $"File '{file.FileName}' exceeds the 10MB size limit.");
                        return View(vm);
                    }

                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    await using var stream = new FileStream(filePath, FileMode.CreateNew);
                    await file.CopyToAsync(stream);
                    uploadedImageUrls.Add($"/uploads/destinations/{fileName}");
                }
            }

            var allImageUrls = uploadedImageUrls
                .Concat(externalImageUrls)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            string? photoUrlsCombined = allImageUrls.Any() ? string.Join("\n", allImageUrls) : null;
            string? daysCombined = vm.SelectedDays.Any() ? string.Join(", ", vm.SelectedDays) : null;

            var destination = new Destination
            {
                Name = vm.Name.Trim(),
                ArabicName = string.IsNullOrWhiteSpace(vm.ArabicName) ? null : vm.ArabicName.Trim(),
                City = vm.City.Trim(),
                Category = vm.Category.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                Tags = string.IsNullOrWhiteSpace(vm.Tags) ? null : vm.Tags.Trim(),
                TicketRequired = vm.TicketRequired,
                EgyptianPrice = vm.EgyptianPrice,
                StudentEgyptianPrice = vm.StudentEgyptianPrice,
                ForeignPrice = vm.ForeignPrice,
                StudentForeignPrice = vm.StudentForeignPrice,
                Days = daysCombined,
                OpenAt = vm.OpenAt,
                CloseAt = vm.CloseAt,
                Booking = vm.Booking,
                PhotoUrls = photoUrlsCombined,
                Location = new NetTopologySuite.Geometries.Point(vm.Longitude, vm.Latitude) { SRID = 4326 },
                Status = "Active",
                Visits = 0,
                Rating = 0m
            };

            // 1. Create Feature in ArcGIS FIRST (Source of Truth)
            var (arcgisSuccess, arcgisError, objectId, createdId) = await _arcgisSync.AddDestinationToArcGISAsync(destination);

            if (!arcgisSuccess)
            {
                ModelState.AddModelError(string.Empty, $"Unable to create the Destination in ArcGIS: {arcgisError}. Please try again.");
                return View(vm);
            }

            // 2. ArcGIS confirmed success -> Sync local DB to ensure local IDs match remote layer
            if (createdId.HasValue)
            {
                destination.Id = createdId.Value;
            }

            // Pull fresh or save to ensure synchronization across website
            await _arcgisSync.SyncDestinationsFromArcGIS();

            TempData["ArcGISMessage"] = $"Destination '{destination.Name}' was successfully created in ArcGIS and synced locally!";
            TempData["ArcGISMessageType"] = "success";

            return RedirectToAction("Index", new { section = "destinations" });
        }
    }
}
