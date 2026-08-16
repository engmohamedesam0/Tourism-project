using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    /// <summary>
    /// Admin content moderation for the generic review system (SiteReview).
    /// Admins can view every review with its Tourist author and target entity,
    /// filter them, and delete inappropriate content.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class AdminReviewController : Controller
    {
        private readonly TouristContext _context;
        private readonly IReviewService _reviewService;

        public AdminReviewController(TouristContext context, IReviewService reviewService)
        {
            _context = context;
            _reviewService = reviewService;
        }

        public IActionResult Index(string? entityType, int? rating, string? search)
        {
            var reviews = _context.SiteReviews
                .Include(r => r.Tourist)
                    .ThenInclude(t => t.ApplicationUser)
                .OrderByDescending(r => r.CreatedDate)
                .ToList();

            // Resolve the display name of every referenced entity in one pass.
            var destIds = reviews.Where(r => r.DestinationId.HasValue).Select(r => r.DestinationId!.Value).Distinct().ToList();
            var branchIds = reviews.Where(r => r.BranchId.HasValue).Select(r => r.BranchId!.Value).Distinct().ToList();
            var rewardIds = reviews.Where(r => r.RewardId.HasValue).Select(r => r.RewardId!.Value).Distinct().ToList();
            var missionIds = reviews.Where(r => r.MissionId.HasValue).Select(r => r.MissionId!.Value).Distinct().ToList();
            var tripIds = reviews.Where(r => r.TripPlanId.HasValue).Select(r => r.TripPlanId!.Value).Distinct().ToList();

            var destNames = _context.Destinations.Where(d => destIds.Contains(d.Id)).ToDictionary(d => d.Id, d => d.Name);
            var branchNames = _context.Branches.Where(b => branchIds.Contains(b.Id)).ToDictionary(b => b.Id, b => b.Name);
            var rewardNames = _context.Rewards.Where(r => rewardIds.Contains(r.Id)).ToDictionary(r => r.Id, r => r.Title);
            var missionNames = _context.Missions.Where(m => missionIds.Contains(m.Id)).ToDictionary(m => m.Id, m => m.Title);
            var tripNames = _context.TripPlans.Where(t => tripIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Title);

            var rows = reviews.Select(r => new AdminReviewRowVM
            {
                Id = r.Id,
                TouristName = r.Tourist?.Name ?? "Tourist",
                TouristEmail = r.Tourist?.ApplicationUser?.Email,
                EntityType = r.EntityType,
                EntityName = r.EntityType switch
                {
                    "Destination" => destNames.TryGetValue(r.DestinationId ?? 0, out var dn) ? dn : "(deleted)",
                    "Branch" => branchNames.TryGetValue(r.BranchId ?? 0, out var bn) ? bn : "(deleted)",
                    "Reward" => rewardNames.TryGetValue(r.RewardId ?? 0, out var rn) ? rn : "(deleted)",
                    "Mission" => missionNames.TryGetValue(r.MissionId ?? 0, out var mn) ? mn : "(deleted)",
                    "TripPlan" => tripNames.TryGetValue(r.TripPlanId ?? 0, out var tn) ? tn : "(deleted)",
                    _ => "(unknown)"
                },
                EntityId = r.EntityType switch
                {
                    "Destination" => r.DestinationId ?? 0,
                    "Branch" => r.BranchId ?? 0,
                    "Reward" => r.RewardId ?? 0,
                    "Mission" => r.MissionId ?? 0,
                    "TripPlan" => r.TripPlanId ?? 0,
                    _ => 0
                },
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedDate = r.CreatedDate
            }).ToList();

            // Filters
            if (!string.IsNullOrWhiteSpace(entityType))
                rows = rows.Where(r => string.Equals(r.EntityType, entityType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
                rows = rows.Where(r => r.Rating == rating.Value).ToList();
            if (!string.IsNullOrWhiteSpace(search))
                rows = rows.Where(r =>
                    r.TouristName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.EntityName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                    (r.Comment?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)).ToList();

            var vm = new AdminReviewListVM
            {
                Reviews = rows,
                TotalCount = rows.Count,
                AverageRating = rows.Count > 0 ? rows.Average(r => (double)r.Rating) : (double?)null,
                EntityTypes = new[] { "Destination", "Branch", "Reward", "Mission", "TripPlan" },
                EntityType = entityType,
                Rating = rating,
                Search = search
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var review = _context.SiteReviews.FirstOrDefault(r => r.Id == id);
            if (review == null) return NotFound();

            var destinationId = review.DestinationId;

            _context.SiteReviews.Remove(review);
            _context.SaveChanges();

            // Keep the destination's displayed rating in sync after moderation.
            if (destinationId.HasValue)
                _reviewService.SyncDestinationRating(destinationId.Value);

            TempData["AdminReviewMessage"] = "Review deleted successfully.";
            TempData["AdminReviewMessageType"] = "success";
            return RedirectToAction(nameof(Index));
        }
    }
}
