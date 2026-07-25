using System.Globalization;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    // Metrics shown on the sponsor dashboard. All counts are already scoped to
    // the signed-in sponsor's SponsorId by the controller query.
    public class SponsorDashboardVM
    {
        public int SponsorId { get; set; }

        // Sponsor identity / context
        public string? SponsorName { get; set; }
        public string? SponsorType { get; set; }
        public string? SponsorAddress { get; set; }
        public string? SponsorContact { get; set; }

        // Existing KPIs
        public int RedeemedCount { get; set; }
        public int RewardViewCount { get; set; }
        public string? MostWantedRewardTitle { get; set; }
        public int MostWantedRewardRedemptions { get; set; }
        public string? MostWantedBranchName { get; set; }
        public bool RatingAvailable { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        // Branch / reward aggregates (reuses SponsorBranchController.Index pattern)
        public int TotalBranches { get; set; }
        public int TotalRewards { get; set; }
        public int TotalRedemptions { get; set; }
        public string? MostActiveBranchName { get; set; }

        // Yearly stats reused from Reports logic
        public List<MonthlyStatRow> MonthlyStats { get; set; } = new();
        public List<TopRewardRow> DashboardTopRewards { get; set; } = new();

        // Branches for map panel (lat/lng for fitBounds, popup wiring)
        public List<BranchMapPoint> SponsorBranches { get; set; } = new();
    }

    public class BranchMapPoint
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
