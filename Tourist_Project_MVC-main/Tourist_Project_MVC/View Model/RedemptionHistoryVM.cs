using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    // One row of the sponsor's redemption history table.
    public class RedemptionHistoryRow
    {
        public int Id { get; set; }
        public string TouristName { get; set; } = string.Empty;
        public string RewardTitle { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public DateTime RedemptionDate { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // View model for the redemption history page: the filtered rows plus the
    // current filter selections and the dropdowns used by the filter bar.
    public class RedemptionHistoryVM
    {
        public List<RedemptionHistoryRow> Rows { get; set; } = new();

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RewardId { get; set; }
        public string? Status { get; set; }

        // Dropdown sources (scoped to the signed-in sponsor).
        public List<Reward> Rewards { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
    }
}
