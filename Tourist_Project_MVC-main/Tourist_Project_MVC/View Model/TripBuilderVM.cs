using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.View_Model
{
    // View model for the tourist self-service trip builder (Step 3).
    public class TripBuilderVM
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        // Planning input captured on the draft/active trip.
        [Range(1, 100, ErrorMessage = "Companions must be at least 1.")]
        public int? Companions { get; set; }

        // One row per destination offered in the picker; Selected drives inclusion.
        public List<TripStopVM> Stops { get; set; } = new List<TripStopVM>();
    }

    public class TripStopVM
    {
        public int DestinationId { get; set; }
        public string DestinationName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        // Filter metadata used by the client-side planning filter (Item 4).
        public string? Category { get; set; }
        public string? Status { get; set; }
        public decimal? TicketPrice { get; set; }

        // Map coordinates (Destination.Location is a NetTopologySuite Point: Y = lat, X = lng).
        public double Lat { get; set; }
        public double Lng { get; set; }

        public bool Selected { get; set; }

        [DataType(DataType.Date)]
        public DateTime ArrivalDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);
    }
}
