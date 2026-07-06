using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class LoginViewModel
    {
        [DisplayName("User Name")]
        public string UserName { get; set; }

        [DataType(DataType.Password)]
        [DisplayName("Password")]
        public string UserPassword { get; set; }
        [DisplayName("Remember Me")]
        public bool RememberMe { get; set; }
    }
}
