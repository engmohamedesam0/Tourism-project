using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ISiteReviewRepository : IRepository<SiteReview>
    {
        IEnumerable<SiteReview> GetForDestination(int destinationId, int take, int skip);
        IEnumerable<SiteReview> GetForTripPlan(int tripPlanId, int take, int skip);
        IEnumerable<SiteReview> GetForReward(int rewardId, int take, int skip);
        IEnumerable<SiteReview> GetForBranch(int branchId, int take, int skip);
        int GetCountForDestination(int destinationId);
        int GetCountForTripPlan(int tripPlanId);
        int GetCountForReward(int rewardId);
    }
}
