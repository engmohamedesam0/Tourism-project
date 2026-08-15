using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.Models
{
    // A public/emergency utility available in Egypt (police stations, fire
    // stations, hospitals, pharmacies). Managed by Admins and browsable by
    // every visitor; each utility may carry its own map location.
    public class Utility
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        // "Police Station" | "Fire Station" | "Hospital" | "Pharmacy"
        [Required]
        public string Type { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? City { get; set; }

        public string? ContactNumber { get; set; }

        // Optional 24/7 opening-hours text (e.g. "24 hours", "8 AM - 10 PM").
        public string? OpenHours { get; set; }

        public Point Location { get; set; } = null!;
    }
}
