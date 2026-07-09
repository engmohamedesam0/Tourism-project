using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class SponsorReviewListVM
    {
        public int SponsorId { get; set; }
        public bool RatingAvailable { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public List<ReviewListItem> Reviews { get; set; } = new();
        public int? SelectedRating { get; set; }
    }

    public class ReviewListItem
    {
        public int Id { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string TouristName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
