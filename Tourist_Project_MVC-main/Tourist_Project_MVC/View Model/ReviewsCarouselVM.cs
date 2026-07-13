namespace Tourist_Project_MVC.View_Model
{
    public class ReviewsCarouselVM
    {
        public string Title { get; set; } = "Traveler Reviews";
        public string TargetTitle { get; set; } = string.Empty;
        public List<ReviewsCarouselItemVM> Items { get; set; } = new();
        public bool CanAddReview { get; set; }
        public int TargetId { get; set; }
        public string TargetType { get; set; } = string.Empty;
    }

    public class ReviewsCarouselItemVM
    {
        public string TouristName { get; set; } = string.Empty;
        public string? TouristPhotoPath { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
