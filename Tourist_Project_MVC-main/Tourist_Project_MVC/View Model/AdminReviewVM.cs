namespace Tourist_Project_MVC.View_Model
{
    public class AdminReviewListVM
    {
        public List<AdminReviewRowVM> Reviews { get; set; } = new();

        public int TotalCount { get; set; }

        public double? AverageRating { get; set; }

        public string[] EntityTypes { get; set; } = Array.Empty<string>();

        public string? EntityType { get; set; }

        public int? Rating { get; set; }

        public string? Search { get; set; }
    }

    public class AdminReviewRowVM
    {
        public int Id { get; set; }

        public string TouristName { get; set; } = string.Empty;

        public string? TouristEmail { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public string? EntityName { get; set; }

        public int EntityId { get; set; }

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
