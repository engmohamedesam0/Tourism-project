using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class UserProgress
    {
        [Key]
        public int TouristId { get; set; }

        [ForeignKey("TouristId")]
        public Tourist? Tourist { get; set; }

        public int CurrentXP { get; set; }

        public int CurrentLevel { get; set; }

        public int CompletedTrips { get; set; }

        public int CompletedMissions { get; set; }

        public int VisitedPlaces { get; set; }

        public int UploadedPhotos { get; set; }

        public int ReviewsCount { get; set; }

        public int LoginStreak { get; set; }

        public DateTime? LastLoginDate { get; set; }
    }
}