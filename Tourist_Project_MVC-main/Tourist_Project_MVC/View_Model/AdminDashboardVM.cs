using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    // Platform-wide admin dashboard metrics.
    public class AdminDashboardVM
    {
        public int TotalTourists { get; set; }
        public int TotalSponsors { get; set; }
        public int TotalDestinations { get; set; }
        public int TotalBranches { get; set; }
        public int TotalRewards { get; set; }

        public int TotalRedemptions { get; set; }
        public int TotalMissionsCompleted { get; set; }

        public bool RatingAvailable { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public List<MonthlyStatRow> MonthlyStats { get; set; } = new();
        public List<TopRewardRow> DashboardTopRewards { get; set; } = new();

        // Branches for the unfiltered map panel.
        public List<BranchMapPoint> AllBranches { get; set; } = new();
    }
}