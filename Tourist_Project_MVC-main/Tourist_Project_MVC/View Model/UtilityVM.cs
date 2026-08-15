using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class UtilityVM
    {
        public int Id { get; set; }

        [Display(Name = "Name")]
        [Required(ErrorMessage = "This field is required")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Type")]
        [Required(ErrorMessage = "This field is required")]
        public string Type { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "City")]
        public string? City { get; set; }

        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [Display(Name = "Open Hours")]
        public string? OpenHours { get; set; }

        [Display(Name = "Latitude")]
        public float Lat { get; set; }

        [Display(Name = "Longitude")]
        public float Long { get; set; }

        // Dropdown options for the type select (populated from UtilityTypes).
        public IEnumerable<SelectListItem> TypeOptions =>
            UtilityTypes.ToSelectList(Type);
    }
}
