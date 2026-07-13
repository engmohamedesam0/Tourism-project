using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace Tourist_Project_MVC.Models
{
    public class Destination
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime? OpeningHours { get; set; }
        public string? Category { get; set; }
        public Point Location { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? TicketPrice { get; set; }

        // Added for the tourist-facing Explore page (Step 1).
        // Nullable so existing rows/seeds remain valid until populated.
        public decimal? Rating { get; set; }
        public string? Tags { get; set; }

        public int Visits { get; set; } = 0;
        public string Status { get; set; } = "Active";

        public List<Mission> Missions { get; set; } = new List<Mission>();
        public List<TripDestination> TripDestinations { get; set; } = new List<TripDestination>();
    }
}