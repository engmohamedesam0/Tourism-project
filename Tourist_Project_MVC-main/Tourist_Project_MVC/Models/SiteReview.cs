using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class SiteReview
    {
        public int Id { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }

        public int? DestinationId { get; set; }
        public Destination? Destination { get; set; }

        public int? TripPlanId { get; set; }
        public TripPlan? TripPlan { get; set; }

        public int? RewardId { get; set; }
        public Reward? Reward { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
