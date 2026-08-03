using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class CreateTouristViewModel
    {
        // User (Identity / Account) Details
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

        [DisplayName("Password")]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DisplayName("Confirm Password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [DisplayName("Phone Number")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [DisplayName("Nationality")]
        [Required]
        public string Nationality { get; set; } = string.Empty;

        [DisplayName("Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }

        // Tourist-Specific Details
        [DisplayName("ID Number")]
        public string? IdNumber { get; set; }

        [DisplayName("Passport Number")]
        public string? Passport { get; set; }

        [DisplayName("Preferred Language")]
        public string? PreferredLanguage { get; set; }

        [DisplayName("Travel Interests")]
        public string? TravelInterests { get; set; }
    }
}
