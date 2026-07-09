using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class TouristRewardVM
    {
        public int PointBalance { get; set; }
        public List<Reward> AvailableRewards { get; set; } = new();
        public List<Redemption> MyRedemptions { get; set; } = new();
        public string? RedeemMessage { get; set; }
        public string? RedeemMessageType { get; set; }
    }
}
