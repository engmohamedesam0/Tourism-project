using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class RewardView
    {
        public int Id { get; set; }
        public int RewardId { get; set; }
        public Reward? Reward { get; set; }

        public string? TouristId { get; set; }

        public DateTime ViewedDate { get; set; } = DateTime.Now;
    }
}
