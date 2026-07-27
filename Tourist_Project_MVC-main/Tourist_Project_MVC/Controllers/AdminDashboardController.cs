using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly TouristContext _context;

        public AdminDashboardController(TouristContext context)
        {
            _context = context;
        }

        public IActionResult Index()
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

            var statBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-people-fill", Color = "black", Value = totalTourists.ToString("N0"), Label = "Total Tourists" },
                new StatBoxItem { IconClass = "bi-building-fill", Color = "black", Value = totalSponsors.ToString("N0"), Label = "Total Sponsors" },
                new StatBoxItem { IconClass = "bi-geo-alt-fill", Color = "black", Value = totalDestinations.ToString("N0"), Label = "Total Destinations" },
                new StatBoxItem { IconClass = "bi-gift-fill", Color = "black", Value = totalRewards.ToString("N0"), Label = "Total Rewards" }
            };

            var vm = new AdminDashboardVM
            {
                TotalTourists = totalTourists,
                TotalSponsors = totalSponsors,
                TotalDestinations = totalDestinations,
                TotalBranches = totalBranches,
                TotalRewards = totalRewards,
                TotalRedemptions = totalRedemptions,
                TotalMissionsCompleted = totalMissionsCompleted,
                RatingAvailable = ratingAvailable,
                AverageRating = averageRating,
                ReviewCount = reviewCount,
                MonthlyStats = monthlyStats,
                DashboardTopRewards = dashboardTopRewards,
                AllBranches = allBranches
            };

            ViewBag.StatBoxes = statBoxes;

            return View("Index", vm);
        }
    }
}