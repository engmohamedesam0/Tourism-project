using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class RegisterViewModel
    {
        // Account type chooser: "Tourist" (default) or "Sponsor".
        [DisplayName("I am registering as a")]
        public string AccountType { get; set; } = "Tourist";

        [DisplayName("User Name")]
        public string UserName { get; set; }
        [DisplayName("Email")]
        public string UserEmail { get; set; }
        [DisplayName("Password")]

        [DataType(DataType.Password)]
        public string Password { get; set; }
        [DataType(DataType.Password)]
        [DisplayName("Confirm Password")]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }

        // ---- Sponsor-only fields (reuse Sponsor entity fields) ----
        [DisplayName("Business Name")]
        public string? BusinessName { get; set; }

        [DisplayName("Business Type / Category")]
        public string? SponsorType { get; set; }

        [DisplayName("Business Address")]
        public string? SponsorAddress { get; set; }

        [DisplayName("Contact Number")]
        public int? ContactNumber { get; set; }
    }
}
