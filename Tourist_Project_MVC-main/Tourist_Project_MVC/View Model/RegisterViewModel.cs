using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class RegisterViewModel
    {
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
    }
}
