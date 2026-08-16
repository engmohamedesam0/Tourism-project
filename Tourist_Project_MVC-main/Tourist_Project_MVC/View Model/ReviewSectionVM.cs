namespace Tourist_Project_MVC.View_Model
{
    /// <summary>
    /// Data backing the shared Rating &amp; Reviews section shown on entity
    /// details pages (Destination, Branch, Reward, Mission, TripPlan).
    /// </summary>
    public class EntityReviewSectionVM
    {
        /// <summary>Entity kind: "Destination", "Branch", "Reward", "Mission", "TripPlan".</summary>
        public string TargetType { get; set; } = string.Empty;

        public int TargetId { get; set; }

        public string TargetTitle { get; set; } = string.Empty;

        public double? AverageRating { get; set; }

        public int ReviewCount { get; set; }

        /// <summary>True when the current visitor is an authenticated Tourist and may post a review.</summary>
        public bool CanAddReview { get; set; }

        public List<EntityReviewItemVM> Items { get; set; } = new();
    }

    public class EntityReviewItemVM
    {
        public int Id { get; set; }

        public string TouristName { get; set; } = string.Empty;

        public string? TouristPhotoPath { get; set; }

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
