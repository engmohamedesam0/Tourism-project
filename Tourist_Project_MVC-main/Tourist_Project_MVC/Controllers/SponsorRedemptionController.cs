using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Sponsor")]
    public class SponsorRedemptionController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly TouristContext _context;

        public SponsorRedemptionController(ISponsorRepository sponsorRepo, IRewardRepository rewardRepo, TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _rewardRepo = rewardRepo;
            _context = context;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        // Redemption history for the signed-in sponsor, filterable by date
        // range, reward and status. All rows are scoped to the sponsor's rewards.
        public IActionResult Index(DateTime? fromDate, DateTime? toDate, int? rewardId, string? status)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var sponsorId = sponsor.Id;

            // Top stat-box row (scoped to this sponsor, query-level aggregates).
            var now = DateTime.Now;
            var totalRedemptions = _context.Redemptions.Count(r => r.Reward != null && r.Reward.SponsorId == sponsorId);
            var redemptionsThisMonth = _context.Redemptions.Count(r =>
                r.Reward != null && r.Reward.SponsorId == sponsorId &&
                r.RedemptionDate.Year == now.Year && r.RedemptionDate.Month == now.Month);
            var uniqueTourists = _context.Redemptions
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .Select(r => r.TouristId)
                .Distinct()
                .Count();

            var topRewardId = _context.Redemptions
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .GroupBy(r => r.RewardId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
            var topRewardTitle = topRewardId > 0
                ? _context.Rewards.Where(rw => rw.Id == topRewardId).Select(rw => rw.Title).FirstOrDefault()
                : null;

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-receipt-fill", Color = "blue", Value = totalRedemptions.ToString("N0"), Label = "Total Redemptions" },
                new StatBoxItem { IconClass = "bi-calendar-month-fill", Color = "green", Value = redemptionsThisMonth.ToString("N0"), Label = "Redemptions This Month" },
                new StatBoxItem { IconClass = "bi-people-fill", Color = "gold", Value = uniqueTourists.ToString("N0"), Label = "Unique Tourists Served" },
                new StatBoxItem { IconClass = "bi-trophy-fill", Color = "purple", Value = topRewardTitle ?? "—", Label = "Most Redeemed Reward" }
            };

            // Distinct statuses present on this sponsor's redemptions, for the filter dropdown.
            var statuses = _context.Redemptions
                .Include(r => r.Reward)
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .Select(r => r.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var redemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Include(r => r.Tourist)
                .Include(r => r.Branch)
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .AsQueryable();

            if (fromDate.HasValue)
                redemptions = redemptions.Where(r => r.RedemptionDate >= fromDate.Value);

            if (toDate.HasValue)
                redemptions = redemptions.Where(r => r.RedemptionDate <= toDate.Value);

            if (rewardId.HasValue)
                redemptions = redemptions.Where(r => r.RewardId == rewardId.Value);

            if (!string.IsNullOrEmpty(status))
                redemptions = redemptions.Where(r => r.Status == status);

            var rows = redemptions
                .OrderByDescending(r => r.RedemptionDate)
                .Select(r => new RedemptionHistoryRow
                {
                    Id = r.Id,
                    TouristName = r.Tourist != null ? r.Tourist.Name : "—",
                    RewardTitle = r.Reward != null ? r.Reward.Title : "—",
                    BranchName = r.Branch != null ? r.Branch.Name : null,
                    RedemptionDate = r.RedemptionDate,
                    Code = r.Code,
                    Status = r.Status
                })
                .ToList();

            var vm = new RedemptionHistoryVM
            {
                Rows = rows,
                FromDate = fromDate,
                ToDate = toDate,
                RewardId = rewardId,
                Status = status,
                Rewards = _rewardRepo.GetBySponsorId(sponsorId).ToList(),
                Statuses = statuses
            };

            return View("Index", vm);
        }
    }
}
