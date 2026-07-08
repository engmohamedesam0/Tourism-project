using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Sponsor")]
    public class SponsorPortalController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly TouristContext _context;

        public SponsorPortalController(ISponsorRepository sponsorRepo, TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _context = context;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        public IActionResult Index()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null)
                return RedirectToAction("CompleteProfile");

            return View("Index", sponsor);
        }

        public IActionResult CompleteProfile()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor != null)
                return RedirectToAction("Index");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteProfile(CompleteSponsorProfileViewModel vm)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;

            var existing = _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
            if (existing != null)
                return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                var sponsor = new Sponsor
                {
                    Name = vm.BusinessName,
                    Type = vm.SponsorType,
                    Address = vm.SponsorAddress,
                    ContactNumber = vm.ContactNumber,
                    Email = email ?? string.Empty,
                    ApplicationUserId = userId
                };

                _sponsorRepo.Add(sponsor);
                _sponsorRepo.Save();
                return RedirectToAction("Index");
            }

            return View(vm);
        }

        public IActionResult Dashboard()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null)
                return RedirectToAction("CompleteProfile");

            var sponsorId = sponsor.Id;

            var redeemedCount = _context.Redemptions
                .Count(r => r.Reward != null && r.Reward.SponsorId == sponsorId);

            var rewardViewCount = _context.RewardViews
                .Count(v => v.Reward != null && v.Reward.SponsorId == sponsorId);

            var mostWantedTitle = (string?)null;
            var mostWantedCount = 0;
            var mostWantedBranch = (string?)null;

            var sponsorRedemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Include(r => r.Branch)
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .ToList();

            var topRewardGroup = sponsorRedemptions
                .GroupBy(r => r.RewardId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (topRewardGroup != null)
            {
                mostWantedTitle = topRewardGroup.First().Reward?.Title;
                mostWantedCount = topRewardGroup.Count();

                var topBranch = topRewardGroup
                    .Where(r => r.BranchId.HasValue)
                    .GroupBy(r => r.BranchId!.Value)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();

                if (topBranch != null)
                    mostWantedBranch = topBranch.First().Branch?.Name;
            }

            var reviews = _context.Reviews
                .Where(rv => rv.SponsorId == sponsorId)
                .ToList();

            var vm = new SponsorDashboardVM
            {
                SponsorId = sponsorId,
                RedeemedCount = redeemedCount,
                RewardViewCount = rewardViewCount,
                MostWantedRewardTitle = mostWantedTitle,
                MostWantedRewardRedemptions = mostWantedCount,
                MostWantedBranchName = mostWantedBranch,
                RatingAvailable = reviews.Any(),
                AverageRating = reviews.Any() ? reviews.Average(rv => rv.Rating) : (double?)null,
                ReviewCount = reviews.Count
            };

            return View("Dashboard", vm);
        }

        public IActionResult Reports()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null)
                return RedirectToAction("CompleteProfile");

            var sponsorId = sponsor.Id;
            var year = DateTime.Now.Year;

            var redemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId &&
                            r.RedemptionDate.Year == year)
                .ToList();

            var rewardViews = _context.RewardViews
                .Include(v => v.Reward)
                .Where(v => v.Reward != null && v.Reward.SponsorId == sponsorId &&
                            v.ViewedDate.Year == year)
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

            var reportRows = Enumerable.Range(1, 12)
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

            var topRewards = redemptions
                .GroupBy(r => r.Reward!.Id)
                .Select(g => new TopRewardRow
                {
                    RewardTitle = g.First().Reward!.Title,
                    Redemptions = g.Count(),
                    Views = rewardViews.Count(v => v.RewardId == g.Key)
                })
                .OrderByDescending(t => t.Redemptions)
                .ToList();

            var vm = new SponsorReportsVM
            {
                CurrentSponsorName = sponsor.Name,
                Year = year,
                ReportRows = reportRows,
                TopRewards = topRewards
            };

            return View("Reports", vm);
        }
    }
}
