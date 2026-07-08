using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Reward
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RewardType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PointsRequired { get; set; }
        public int QuantityAvailable { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string Status { get; set; } = "Active";

        [ForeignKey("SponsorId")]
        public int SponsorId { get; set; }
        public Sponsor? Sponsor { get; set; }

        public List<Redemption>? Redemptions { get; set; }

        public List<RewardBranch>? RewardBranches { get; set; }
    }
}
