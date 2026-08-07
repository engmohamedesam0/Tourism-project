using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace Tourist_Project_MVC.Models
{
    public class Destination
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ArabicName { get; set; }
        public string City { get; set; } = string.Empty;
        public DateTime? OpeningHours { get; set; }
        public int? OpenAt { get; set; }
        public int? CloseAt { get; set; }
        public string? Category { get; set; }
        public Point Location { get; set; } = null!;
        public string? Description { get; set; }
        public string? PhotoUrls { get; set; }
        public decimal? TicketPrice { get; set; }
        public string? TicketRequired { get; set; }
        public int? ForeignPrice { get; set; }
        public int? StudentForeignPrice { get; set; }
        public int? EgyptianPrice { get; set; }
        public int? StudentEgyptianPrice { get; set; }
        public string? Days { get; set; }
        public string? Booking { get; set; }

        // Added for the tourist-facing Explore page (Step 1).
        // Nullable so existing rows/seeds remain valid until populated.
        public decimal? Rating { get; set; }
        public string? Tags { get; set; }

        public int Visits { get; set; } = 0;
        public string Status { get; set; } = "Active";

        [NotMapped]
        public List<string> PhotoUrlList => string.IsNullOrWhiteSpace(PhotoUrls)
            ? new List<string>()
            : PhotoUrls.Split(

                new[] { "\r\n", "\n", "|" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        public List<Mission> Missions { get; set; } = new List<Mission>();
        public List<TripDestination> TripDestinations { get; set; } = new List<TripDestination>();
    }
}