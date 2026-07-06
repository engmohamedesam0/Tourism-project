using Microsoft.AspNetCore.Identity;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class UserRoleViewModel
    {
        public string UserId { get; set; }
        public string RoleName { get; set; }
        
        public List<ApplicationUser>? Users { get; set; }
        public List<IdentityRole>? Roles { get; set; }
    }
}
