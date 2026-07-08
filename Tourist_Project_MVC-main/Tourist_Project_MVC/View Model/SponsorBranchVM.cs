using System.ComponentModel.DataAnnotations;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class SponsorBranchVM
    {
        public int Id { get; set; }

        [Display(Name = "Branch Name")]
        [Required(ErrorMessage = "This Field required")]
        public string Name { get; set; }

        [Display(Name = "Address")]
        [Required(ErrorMessage = "This Field required")]
        public string Address { get; set; }

        [Display(Name = "Latitude")]
        public float Lat { get; set; }

        [Display(Name = "Longitude")]
        public float Long { get; set; }

        [Display(Name = "Phone Number")]
        public int? ContactNumber { get; set; }

        public int SponsorId { get; set; }
    }
}
