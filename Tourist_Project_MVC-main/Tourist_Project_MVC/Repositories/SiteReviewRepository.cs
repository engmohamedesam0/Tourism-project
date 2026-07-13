using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class SiteReviewRepository : Repository<SiteReview>, ISiteReviewRepository
    {
        public SiteReviewRepository(TouristContext context) : base(context) { }

        public IEnumerable<SiteReview> GetForDestination(int destinationId, int take, int skip)
        {
            return _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.DestinationId == destinationId)
                .OrderByDescending(r => r.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public IEnumerable<SiteReview> GetForTripPlan(int tripPlanId, int take, int skip)
        {
            return _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.TripPlanId == tripPlanId)
                .OrderByDescending(r => r.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public IEnumerable<SiteReview> GetForReward(int rewardId, int take, int skip)
        {
            return _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.RewardId == rewardId)
                .OrderByDescending(r => r.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public IEnumerable<SiteReview> GetForBranch(int branchId, int take, int skip)
        {
            return _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.BranchId == branchId)
                .OrderByDescending(r => r.CreatedDate)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public int GetCountForDestination(int destinationId)
        {
            return _context.SiteReviews.Count(r => r.DestinationId == destinationId);
        }

        public int GetCountForTripPlan(int tripPlanId)
        {
            return _context.SiteReviews.Count(r => r.TripPlanId == tripPlanId);
        }

        public int GetCountForReward(int rewardId)
        {
            return _context.SiteReviews.Count(r => r.RewardId == rewardId);
        }
    }
}
