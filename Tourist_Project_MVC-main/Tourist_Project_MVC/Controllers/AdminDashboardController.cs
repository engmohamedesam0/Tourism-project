using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminDashboard")]
    public class AdminDashboardController : Controller
    {
        private readonly TouristContext _context;
        private static readonly string[] Sections = new[]
        {
            "overview", "tourists", "missions", "sponsors",
            "destinations", "rewards", "levels", "badges", "support", "reviews"
        };

        public AdminDashboardController(TouristContext context)
        {
            _context = context;
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
                .ToList();

            vm.TouristSection.Total = allTourists.Count();
            
            var active = allTourists.Count(t => t.Status == "Active");
            var inactive = allTourists.Count(t => t.Status == "Inactive");
            var suspended = allTourists.Count(t => t.Status == "Suspended");
            
            vm.TouristSection.Active = active;
            vm.TouristSection.Inactive = inactive;
            vm.TouristSection.Suspended = suspended;

            vm.TouristSection.GrowthPercentage = 12.5; // Mock data
            vm.TouristSection.RetentionRate = 85.2; // Mock data
            vm.TouristSection.AverageSessionDuration = "14m 30s"; // Mock data
            vm.TouristSection.AverageMissionsCompleted = allTourists.Any() ? Math.Round(allTourists.Average(t => t.UserMissions?.Count ?? 0), 1) : 0;

            var nationalityGroups = allTourists
                .GroupBy(t => string.IsNullOrEmpty(t.Nationality) ? "Unknown" : t.Nationality)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(r => r.Count)
                .Take(10)
                .ToList()
                .Select(r => new AdminDashboardVM.NameCountRow(r.Name, "🌍", r.Count))
                .ToList();

            var statusGroups = new List<AdminDashboardVM.NameCountRow>
            {
                new AdminDashboardVM.NameCountRow("Active", "✅", active),
                new AdminDashboardVM.NameCountRow("Inactive", "💤", inactive),
                new AdminDashboardVM.NameCountRow("Suspended", "🚫", suspended)
            };

            vm.TouristSection.NationalityBreakdown = nationalityGroups;
            vm.TouristSection.StatusBreakdown = statusGroups;
            
            // Mock Line Chart Data
            var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            vm.TouristSection.MonthsLabels = months.Take(DateTime.Now.Month).ToList();
            
            var rnd = new Random(42);
            for(int i=0; i<vm.TouristSection.MonthsLabels.Count; i++) {
                vm.TouristSection.ActiveHistory.Add(rnd.Next(50, 200) + (i * 10));
                vm.TouristSection.InactiveHistory.Add(rnd.Next(10, 50));
                vm.TouristSection.SuspendedHistory.Add(rnd.Next(0, 10));
            }

            // Top Destinations (mocking visits)
            var destinations = _context.Destinations.Take(10).ToList();
            foreach(var dest in destinations) {
                vm.TouristSection.TopDestinations.Add(new AdminDashboardVM.NameCountRow(dest.Name, "📍", rnd.Next(100, 1000)));
            }
            vm.TouristSection.TopDestinations = vm.TouristSection.TopDestinations.OrderByDescending(x => x.Count).ToList();

            // Top Tourists by Points
            vm.TouristSection.TopTouristsByPoints = allTourists
                .OrderByDescending(t => t.point_Balance)
                .Take(5)
                .Select(t => new TopTouristRow(t.Name, t.point_Balance, "⭐", "Points"))
                .ToList();

            // Top Tourists by Badges
            vm.TouristSection.TopTouristsByBadges = allTourists
                .OrderByDescending(t => t.UserBadges?.Count ?? 0)
                .Take(5)
                .Select(t => new TopTouristRow(t.Name, t.UserBadges?.Count ?? 0, "🏅", "Badges"))
                .ToList();

            // Top Tourists by Level
            vm.TouristSection.TopTouristsByLevel = allTourists
                .OrderByDescending(t => t.UserProgress?.CurrentLevel ?? 0)
                .Take(5)
                .Select(t => new TopTouristRow(t.Name, t.UserProgress?.CurrentLevel ?? 0, "🏆", "Level"))
                .ToList();
                
            // Recent Activities
            vm.TouristSection.RecentActivities = new List<RecentActivityRow>
            {
                new RecentActivityRow("New Registration", "John Doe joined the platform", "2 hours ago", "bi-person-plus", "text-success"),
                new RecentActivityRow("Mission Completed", "Sarah Smith completed 'Pyramids Tour'", "5 hours ago", "bi-check-circle", "text-primary"),
                new RecentActivityRow("Reward Claimed", "Ahmed Ali claimed 'Free Coffee'", "1 day ago", "bi-gift", "text-warning"),
                new RecentActivityRow("Destination Visited", "Maria visited 'Luxor Temple'", "1 day ago", "bi-geo-alt", "text-info"),
                new RecentActivityRow("Level Up", "Omar reached Level 5", "2 days ago", "bi-trophy", "text-danger"),
            };
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
    }
}
