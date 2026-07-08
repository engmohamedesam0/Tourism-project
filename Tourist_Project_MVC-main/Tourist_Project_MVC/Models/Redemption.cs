using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Redemption
    {
        public int Id { get; set; }
        public int PointsRedeemed { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RedemptionDate { get; set; }

        [ForeignKey("RewardId")]
        public int RewardId { get; set; }
        public Reward? Reward { get; set; }
        [ForeignKey("TouristId")]
        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }

        // The branch where the reward was redeemed. Set at redemption time so
        // the dashboard can report "most-wanted reward, and which branch it was
        // most redeemed at". Nullable: older/branch-less redemptions stay null.
        [ForeignKey("BranchId")]
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
