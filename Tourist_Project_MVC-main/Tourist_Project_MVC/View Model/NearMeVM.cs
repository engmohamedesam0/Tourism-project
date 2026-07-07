using Microsoft.AspNetCore.Mvc.Rendering;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    // A sponsor enriched with distance/rating info for the Near Me list.
    public class SponsorCardVM
    {
        public Sponsor Sponsor { get; set; } = new Sponsor();
        public double DistanceKm { get; set; }
        public double AvgRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class NearMeIndexVM
    {
        public int? DestinationId { get; set; }
        public string? DestinationName { get; set; }
        public List<SelectListItem> Destinations { get; set; } = new();
        public string? Search { get; set; }
        public string? Type { get; set; }
        public string? Sort { get; set; }
        public string? Distance { get; set; }
        public string? Rating { get; set; }
        public List<SponsorCardVM> Cards { get; set; } = new();
    }
}
