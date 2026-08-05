using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class AdminDashboardVM
    {
        public string ActiveSection { get; set; } = "overview";

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
        public List<BranchMapPoint> AllBranches { get; set; } = new();

        // Tourism intelligence aggregates built from existing platform data.
        public int TotalTrips { get; set; }
        public int RecordedDestinationVisits { get; set; }
        public int HighPotentialDestinations { get; set; }
        public int EngagedTourists { get; set; }
        public double EngagementRate { get; set; }
        public double AverageStayDays { get; set; }
        public double DiscoveryPotential { get; set; }
        public string MostVisitedDestination { get; set; } = "No recorded visits";
        public List<TourismSeriesPoint> ActivityTrend { get; set; } = new();
        public List<TourismDestinationRow> DestinationPerformance { get; set; } = new();
        public List<TourismDestinationRow> HiddenDestinations { get; set; } = new();
        public List<TourismFlowRow> TourismFlows { get; set; } = new();
        public List<TourismCongestionRow> Congestion { get; set; } = new();
        public List<TourismInsightRow> Insights { get; set; } = new();
        public List<TourismActivityRow> RecentTouristActivity { get; set; } = new();
        public List<string> DestinationOptions { get; set; } = new();
        public List<string> RegionOptions { get; set; } = new();

        public TouristSectionVM TouristSection { get; set; } = new();
        public MissionSectionVM MissionSection { get; set; } = new();
        public SponsorSectionVM SponsorSection { get; set; } = new();
        public DestinationSectionVM DestinationSection { get; set; } = new();
        public RewardSectionVM RewardSection { get; set; } = new();
        public LevelSectionVM LevelSection { get; set; } = new();
        public BadgeSectionVM BadgeSection { get; set; } = new();
        public SupportSectionVM SupportSection { get; set; } = new();
        public ReviewSectionVM ReviewSection { get; set; } = new();

        public record NameCountRow(string Name, string Icon, int Count);
    }

    public class TouristSectionVM
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Inactive { get; set; }
        public int Suspended { get; set; }
        
        public double GrowthPercentage { get; set; }
        public double RetentionRate { get; set; }
        public string AverageSessionDuration { get; set; } = "0m";
        public double AverageMissionsCompleted { get; set; }
        
        public List<AdminDashboardVM.NameCountRow> NationalityBreakdown { get; set; } = new();
        public List<AdminDashboardVM.NameCountRow> StatusBreakdown { get; set; } = new();
        
        public List<int> ActiveHistory { get; set; } = new();
        public List<int> InactiveHistory { get; set; } = new();
        public List<int> SuspendedHistory { get; set; } = new();
        public List<string> MonthsLabels { get; set; } = new();
        
        public List<AdminDashboardVM.NameCountRow> TopDestinations { get; set; } = new();
        
        public List<TopTouristRow> TopTouristsByPoints { get; set; } = new();
        public List<TopTouristRow> TopTouristsByBadges { get; set; } = new();
        public List<TopTouristRow> TopTouristsByLevel { get; set; } = new();
        
        public List<RecentActivityRow> RecentActivities { get; set; } = new();
    }

    public record TopTouristRow(string Name, int Value, string Icon, string Subtext = "");
    public record RecentActivityRow(string Title, string Description, string TimeAgo, string Icon, string Color);
    public record TourismSeriesPoint(string Label, int Tourists, int Missions);
    public record TourismDestinationRow(string Destination, string Region, int Visitors, double Rating, double Momentum, string Congestion, string Status, bool IsHidden, double Potential);
    public record TourismFlowRow(string From, string To, int Volume);
    public record TourismCongestionRow(string Destination, string Level, int Visitors, double Share);
    public record TourismInsightRow(string Title, string Detail, string Icon, string Tone);
    public record TourismActivityRow(string Title, string Detail, string TimeAgo, string Icon, string Tone);

    public class MissionSectionVM
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Pending { get; set; }
        public List<AdminDashboardVM.NameCountRow> TypeBreakdown { get; set; } = new();
    }

    public class SponsorSectionVM
    {
        public int Total { get; set; }
        public List<AdminDashboardVM.NameCountRow> TypeBreakdown { get; set; } = new();
    }

    public class DestinationSectionVM
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public List<AdminDashboardVM.NameCountRow> CategoryBreakdown { get; set; } = new();
        public List<DestinationAdminRow> Records { get; set; } = new();
    }

    public record DestinationAdminRow(int Id, string Name, string City, string? Category, string Status, int Visits);

    public class RewardSectionVM
    {
        public int Total { get; set; }
        public int Available { get; set; }
        public int TotalRedemptions { get; set; }
        public List<AdminDashboardVM.NameCountRow> TypeBreakdown { get; set; } = new();
    }

    public class LevelSectionVM
    {
        public int TouristsWithProgress { get; set; }
        public List<AdminDashboardVM.NameCountRow> LevelDistribution { get; set; } = new();
    }

    public class BadgeSectionVM
    {
        public int TotalBadges { get; set; }
        public int TotalEarned { get; set; }
        public List<AdminDashboardVM.NameCountRow> RarityBreakdown { get; set; } = new();
    }

    public class SupportSectionVM
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int Resolved { get; set; }
        public List<AdminDashboardVM.NameCountRow> StatusBreakdown { get; set; } = new();
    }

    public class ReviewSectionVM
    {
        public int Total { get; set; }
        public double? AverageRating { get; set; }
        public List<AdminDashboardVM.NameCountRow> RatingDistribution { get; set; } = new();
    }
}
