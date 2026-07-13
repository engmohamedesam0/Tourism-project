namespace Tourist_Project_MVC.View_Model
{
    public class EditTouristProfileVM
    {
        public string? ProfilePicturePath { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? ProfilePicture { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string? PreferredLanguage { get; set; }
        public string? TravelInterests { get; set; }
        public bool NotifyByEmail { get; set; }
        public bool NotifyInApp { get; set; }
    }
}
