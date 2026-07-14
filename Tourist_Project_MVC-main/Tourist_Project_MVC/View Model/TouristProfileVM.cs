using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class TouristProfileVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Nationality { get; set; } = string.Empty;
        public DateTime RegisterDate { get; set; }
        public string? Status { get; set; }
        public int PointBalance { get; set; }
        public string? ProfilePicturePath { get; set; }

        public string LevelLabel { get; set; } = string.Empty;
        public string LevelIcon { get; set; } = string.Empty;
        public int MissionsCompletedCount { get; set; }
        public int PlacesVisitedCount { get; set; }
        public int RewardsRedeemedCount { get; set; }
        public string? FavoriteDestination { get; set; }
        public string? PreferredLanguage { get; set; }
        public string? TravelInterests { get; set; }
        public bool NotifyByEmail { get; set; }
        public bool NotifyInApp { get; set; }
    }
}
