using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class sponsorViewModel
    {
        public int Id { get; set; }
        [Display(Name ="Name of the Brand")]
        [Required(ErrorMessage = "This Field required")]
        public string Name { get; set; }

        [Display(Name="Type of Service")]
        [Required(ErrorMessage = "This Field required")]
        public  string Type { get; set; }

        // Dropdown options for the category select (populated from SponsorCategories).
        public IEnumerable<SelectListItem> TypeOptions =>
            SponsorCategories.ToSelectList(Type);

        [Display(Name ="Address")]
        [Required(ErrorMessage = "This Field required")]
        public string City { get; set; }

        [Display(Name="Phone Number")]
        [Required(ErrorMessage = "This Field required")]
        public int ContactInfo { get; set; }
    }
}
