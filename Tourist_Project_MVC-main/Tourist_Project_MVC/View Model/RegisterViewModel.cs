using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class RegisterViewModel
    {
        // Account type chooser: "Tourist" (default) or "Sponsor".
        [DisplayName("I am registering as a")]
        [Required]
        public string AccountType { get; set; } = "Tourist";

        [DisplayName("Email")]
        [Required]
        [EmailAddress]
        public string UserEmail { get; set; }
        [DisplayName("Password")]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [DataType(DataType.Password)]
        [DisplayName("Confirm Password")]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }

        // ---- Shared profile fields (Tourist + Sponsor) ----
        [DisplayName("First Name")]
        [Required]
        public string FirstName { get; set; }

        [DisplayName("Last Name")]
        [Required]
        public string LastName { get; set; }

        [DisplayName("Phone Number")]
        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [DisplayName("Nationality")]
        [Required]
        public string Nationality { get; set; }

        // Optional profile picture upload (image only).
        [DisplayName("Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }
}
