using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class ResetPasswordViewModel
    {
        [EmailAddress]
        [Required]
        [DisplayName("Email")]
        public string UserEmail { get; set; }
    }
}
