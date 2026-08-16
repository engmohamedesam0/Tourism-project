using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    /// <summary>
    /// Shared logic for the generic review system (SiteReview). Provides the
    /// data needed to render the Rating &amp; Reviews section for any entity
    /// type, validates target entities, and keeps Destination.Rating in sync
    /// with the reviews tourists actually submit.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>Builds the review section for one entity (average, count, latest reviews).</summary>
        EntityReviewSectionVM GetSection(string targetType, int targetId, string targetTitle, bool canAdd);

        /// <summary>Validates that a target entity of the given type really exists.</summary>
        bool EntityExists(string targetType, int targetId);

        /// <summary>Recomputes Destination.Rating from its SiteReviews (average, 2dp).</summary>
        void SyncDestinationRating(int destinationId);
    }

    public class ReviewService : IReviewService
    {
        private readonly TouristContext _context;

        public ReviewService(TouristContext context)
        {
            _context = context;
        }

        public bool EntityExists(string targetType, int targetId)
        {
            return targetType switch
            {
                "Destination" => _context.Destinations.Any(d => d.Id == targetId),
                "Branch" => _context.Branches.Any(b => b.Id == targetId),
                "Reward" => _context.Rewards.Any(r => r.Id == targetId),
                "Mission" => _context.Missions.Any(m => m.Id == targetId),
                "TripPlan" => _context.TripPlans.Any(t => t.Id == targetId),
                _ => false
            };
        }

        public EntityReviewSectionVM GetSection(string targetType, int targetId, string targetTitle, bool canAdd)
        {
            IQueryable<SiteReview> query = _context.SiteReviews
                .Include(r => r.Tourist)
                    .ThenInclude(t => t.ApplicationUser);

            query = targetType switch
            {
                "Destination" => query.Where(r => r.DestinationId == targetId),
                "Branch" => query.Where(r => r.BranchId == targetId),
                "Reward" => query.Where(r => r.RewardId == targetId),
                "Mission" => query.Where(r => r.MissionId == targetId),
                "TripPlan" => query.Where(r => r.TripPlanId == targetId),
                _ => query.Where(r => false)
            };

            var reviews = query.OrderByDescending(r => r.CreatedDate).Take(50).ToList();
            var avg = reviews.Count > 0 ? reviews.Average(r => (double)r.Rating) : (double?)null;

            return new EntityReviewSectionVM
            {
                TargetType = targetType,
                TargetId = targetId,
                TargetTitle = targetTitle,
                AverageRating = avg,
                ReviewCount = reviews.Count,
                CanAddReview = canAdd,
                Items = reviews.Select(r => new EntityReviewItemVM
                {
                    Id = r.Id,
                    TouristName = r.Tourist?.Name ?? "Tourist",
                    TouristPhotoPath = r.Tourist?.ApplicationUser?.ProfilePicturePath,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                }).ToList()
            };
        }

        public void SyncDestinationRating(int destinationId)
        {
            var reviews = _context.SiteReviews
                .Where(r => r.DestinationId == destinationId)
                .ToList();

            var destination = _context.Destinations.FirstOrDefault(d => d.Id == destinationId);
            if (destination == null)
                return;

            destination.Rating = reviews.Count > 0
                ? (decimal?)Math.Round(reviews.Average(r => r.Rating), 2)
                : null;

            _context.SaveChanges();
        }
    }
}
