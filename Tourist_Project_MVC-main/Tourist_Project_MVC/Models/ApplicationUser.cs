using Microsoft.AspNetCore.Identity;

namespace Tourist_Project_MVC.Models
{
    public class ApplicationUser:IdentityUser
    {
        // Shared profile fields collected at registration (Tourist + Sponsor).
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;

        // Relative path under wwwroot (e.g. /uploads/profile-pictures/guid.jpg).
        // Nullable: the upload is explicitly optional.
        public string? ProfilePicturePath { get; set; }
    }
}
