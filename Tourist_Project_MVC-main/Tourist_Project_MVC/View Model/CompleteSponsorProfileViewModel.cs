using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    public class CompleteSponsorProfileViewModel
    {
        [DisplayName("Business Name")]
        [Required]
        public string BusinessName { get; set; } = string.Empty;

        [DisplayName("Business Type / Category")]
        [Required]
        public string SponsorType { get; set; } = string.Empty;

        [DisplayName("Business Address")]
        [Required]
        public string SponsorAddress { get; set; } = string.Empty;

        [DisplayName("Contact Number")]
        [Required]
        public int ContactNumber { get; set; }
    }
}
