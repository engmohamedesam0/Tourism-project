using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    // Metrics shown on the sponsor dashboard. All counts are already scoped to
    // the signed-in sponsor's SponsorId by the controller query.
    public class SponsorDashboardVM
    {
        public int SponsorId { get; set; }

        // Redeemed rewards count: number of Redemptions tied to this sponsor's rewards.
        public int RedeemedCount { get; set; }

        // How many times tourists opened one of this sponsor's reward detail pages.
        public int RewardViewCount { get; set; }

        // Most-wanted reward (most redemptions) and the branch it was most
        // redeemed at. Null when the sponsor has no redemptions yet.
        public string? MostWantedRewardTitle { get; set; }
        public int MostWantedRewardRedemptions { get; set; }
        public string? MostWantedBranchName { get; set; }

        // Average tourist rating across the sponsor's reviews (reuses the
        // existing Review entity, which is stored at the Sponsor level).
        public bool RatingAvailable { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
