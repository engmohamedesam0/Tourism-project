using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Tourist_Project_MVC.View_Model
{
    public class AddDestinationVM
    {
        [Required(ErrorMessage = "Destination Name is required.")]
        [Display(Name = "English Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Arabic Name")]
        public string? ArabicName { get; set; }

        [Required(ErrorMessage = "City / Governorate is required.")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Tags { get; set; }

        // Location
        [Required(ErrorMessage = "Please select a location on the map.")]
        [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Please select a location on the map.")]
        [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
        public double Longitude { get; set; }

        // Ticket Info
        [Display(Name = "Ticket Required")]
        public string TicketRequired { get; set; } = "No"; // "Yes" or "No"

        [Display(Name = "Egyptian Price")]
        [Range(0, 100000)]
        public int? EgyptianPrice { get; set; }

        [Display(Name = "Egyptian Student Price")]
        [Range(0, 100000)]
        public int? StudentEgyptianPrice { get; set; }

        [Display(Name = "Foreign Price")]
        [Range(0, 100000)]
        public int? ForeignPrice { get; set; }

        [Display(Name = "Foreign Student Price")]
        [Range(0, 100000)]
        public int? StudentForeignPrice { get; set; }

        // Opening Days & Hours
        public List<string> SelectedDays { get; set; } = new();

        [Display(Name = "Opening Time (Hour 0-23)")]
        [Range(0, 23)]
        public int? OpenAt { get; set; }

        [Display(Name = "Closing Time (Hour 0-23)")]
        [Range(0, 23)]
        public int? CloseAt { get; set; }

        public string? Booking { get; set; }

        // Images
        [Display(Name = "Destination Images")]
        public List<IFormFile>? ImageFiles { get; set; }
    }
}
