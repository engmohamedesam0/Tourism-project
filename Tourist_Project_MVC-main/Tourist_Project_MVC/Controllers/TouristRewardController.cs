using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User")]
    public class TouristRewardController : Controller
    {
        private readonly ITouristRepository _touristRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly TouristContext _context;

        public TouristRewardController(ITouristRepository touristRepo, IRewardRepository rewardRepo, TouristContext context)
        {
            _touristRepo = touristRepo;
            _rewardRepo = rewardRepo;
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var now = DateTime.Now;
            var availableRewards = _context.Rewards
                .Include(r => r.Sponsor)
                .Include(r => r.RewardBranches)
                .ThenInclude(rb => rb.Branch)
                .Where(r => r.Status == "Active"
                    && r.QuantityAvailable > 0
                    && r.ExpirationDate > now)
                .OrderBy(r => r.PointsRequired)
                .ToList();

            var myRedemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Where(r => r.TouristId == tourist.Id)
                .OrderByDescending(r => r.RedemptionDate)
                .ToList();

            var vm = new TouristRewardVM
            {
                PointBalance = tourist.point_Balance,
                AvailableRewards = availableRewards,
                MyRedemptions = myRedemptions
            };

            var rewardReviews = _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.RewardId != null)
                .OrderByDescending(r => r.CreatedDate)
                .Take(5)
                .ToList();

            ViewBag.RewardsCarousel = new Tourist_Project_MVC.View_Model.ReviewsCarouselVM
            {
                Title = "Reward Reviews",
                TargetTitle = "Points & Rewards",
                Items = rewardReviews.Select(r => new Tourist_Project_MVC.View_Model.ReviewsCarouselItemVM
                {
                    TouristName = r.Tourist?.Name ?? "Tourist",
                    TouristPhotoPath = r.Tourist?.ApplicationUser != null ? r.Tourist.ApplicationUser.ProfilePicturePath : null,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                }).ToList(),
                CanAddReview = true,
                TargetId = 0,
                TargetType = "Reward"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Redeem(int rewardId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var reward = _context.Rewards
                .Include(r => r.RewardBranches)
                .FirstOrDefault(r => r.Id == rewardId);

            if (reward == null)
            {
                TempData["RewardMessage"] = "Reward not found.";
                TempData["RewardMessageType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            if (reward.Status != "Active" || reward.QuantityAvailable <= 0 || reward.ExpirationDate <= DateTime.Now)
            {
                TempData["RewardMessage"] = "This reward is no longer available.";
                TempData["RewardMessageType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            if (tourist.point_Balance < reward.PointsRequired)
            {
                TempData["RewardMessage"] = "Not enough points to redeem this reward.";
                TempData["RewardMessageType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                tourist.point_Balance -= reward.PointsRequired;
                reward.QuantityAvailable -= 1;

                var redemption = new Redemption
                {
                    PointsRedeemed = reward.PointsRequired,
                    Code = GenerateVoucherCode(reward),
                    Status = "Active",
                    RedemptionDate = DateTime.Now,
                    RewardId = reward.Id,
                    TouristId = tourist.Id,
                    BranchId = reward.RewardBranches?.FirstOrDefault()?.BranchId
                };

                _context.Redemptions.Add(redemption);
                _context.SaveChanges();
                transaction.Commit();

                TempData["RewardMessage"] = $"Success! Your voucher code is: <strong>{redemption.Code}</strong>";
                TempData["RewardMessageType"] = "success";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                transaction.Rollback();
                TempData["RewardMessage"] = "Something went wrong during redemption. Please try again.";
                TempData["RewardMessageType"] = "danger";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GenerateVoucherCode(Reward reward)
        {
            var prefix = reward.Title.Length > 4
                ? reward.Title.Substring(0, 4).ToUpperInvariant()
                : reward.Title.ToUpperInvariant();
            var guid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            return $"{prefix}-{guid}";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(int id, [Bind("Rating,Comment")] SiteReview vm)
        {
            var reward = _context.Rewards.FirstOrDefault(r => r.Id == id);
            if (reward == null) return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
            if (tourist == null) return RedirectToAction(nameof(Index));

            if (ModelState.IsValid)
            {
                var review = new SiteReview
                {
                    Rating = vm.Rating,
                    Comment = vm.Comment,
                    RewardId = id,
                    TouristId = tourist.Id,
                    CreatedDate = DateTime.Now
                };

                _context.SiteReviews.Add(review);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
