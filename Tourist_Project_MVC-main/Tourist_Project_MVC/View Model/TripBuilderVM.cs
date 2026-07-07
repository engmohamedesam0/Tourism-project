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

        // Planning inputs captured on the draft/active trip (Items 2 & 4).
        [Range(0, double.MaxValue, ErrorMessage = "Budget must be a positive amount.")]
        public decimal? Budget { get; set; }

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

        public bool Selected { get; set; }

        [DataType(DataType.Date)]
        public DateTime ArrivalDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);
    }
}
