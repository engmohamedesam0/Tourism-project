using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class EditTouristViewModel
    {
        public int Id { get; set; }

        public string? ApplicationUserId { get; set; }

        // --- User Account Data (Source of Truth: ApplicationUser / Users Table) ---
        [DisplayName("First Name")]
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [DisplayName("Last Name")]
        [Required]
        public string LastName { get; set; } = string.Empty;

        [DisplayName("Email")]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [DisplayName("Phone Number")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [DisplayName("Nationality")]
        [Required]
        public string Nationality { get; set; } = string.Empty;

        [DisplayName("New Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }

        public string? ExistingProfilePicturePath { get; set; }

        // --- Tourist Specific Data (Source of Truth: Tourist Table) ---
        [DisplayName("ID Number")]
        public string? IdNumber { get; set; }

        [DisplayName("Passport Number")]
        public string? Passport { get; set; }

        [DisplayName("Point Balance")]
        [Range(0, int.MaxValue)]
        public int point_Balance { get; set; }

        [DisplayName("Status")]
        [Required]
        public string Status { get; set; } = "Active";

        [DisplayName("Preferred Language")]
        public string? PreferredLanguage { get; set; }

        [DisplayName("Travel Interests")]
        public string? TravelInterests { get; set; }

        public DateTime RegisterDate { get; set; }
    }
}
