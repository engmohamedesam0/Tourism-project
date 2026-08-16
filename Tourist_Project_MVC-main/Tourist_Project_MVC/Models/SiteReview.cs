using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    /// <summary>
    /// Generic, entity-typed review used across the platform. One row belongs to
    /// exactly one target entity; the target is identified by the single FK
    /// column that is set (DestinationId, TripPlanId, RewardId, BranchId or
    /// MissionId). <see cref="EntityType"/> derives that target from the model
    /// so controllers/views can handle every entity type uniformly.
    /// </summary>
    public class SiteReview
    {
        public int Id { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>Set automatically whenever a review is created or updated.</summary>
        public DateTime? UpdatedDate { get; set; }

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

        public int? MissionId { get; set; }
        public Mission? Mission { get; set; }

        /// <summary>Target entity type derived from whichever FK column is set.</summary>
        [NotMapped]
        public string EntityType =>
            DestinationId.HasValue ? "Destination"
            : TripPlanId.HasValue ? "TripPlan"
            : RewardId.HasValue ? "Reward"
            : BranchId.HasValue ? "Branch"
            : MissionId.HasValue ? "Mission"
            : "Unknown";
    }
}
