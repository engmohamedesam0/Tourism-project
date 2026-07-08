using System.Globalization;

namespace Tourist_Project_MVC.View_Model
{
    // One row of the monthly breakdown table on the sponsor reports page.
    public class MonthlyStatRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int Redemptions { get; set; }
        public int PointsRedeemed { get; set; }
        public int Views { get; set; }
    }

    // One row of the top-rewards table on the sponsor reports page.
    public class TopRewardRow
    {
        public string RewardTitle { get; set; } = string.Empty;
        public int Redemptions { get; set; }
        public int Views { get; set; }
    }

    // View model for the sponsor reports page: a monthly breakdown plus the
    // sponsor's top-performing rewards, all scoped to the signed-in sponsor.
    public class SponsorReportsVM
    {
        public string CurrentSponsorName { get; set; } = string.Empty;

        public int Year { get; set; }

        public List<MonthlyStatRow> ReportRows { get; set; } = new();

        public List<TopRewardRow> TopRewards { get; set; } = new();
    }
}
