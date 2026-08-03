namespace Tourist_Project_MVC.DTOs
{
    // Trip planning payloads for the React Native app (see MobileTripController).
    // The website uses TripBuilderVM instead; mobile posts a plain id list and
    // lets the server derive per-stop dates from the trip's date range.
    public class CreateTripDto
    {
        public string Title { get; set; } = String.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? Budget { get; set; }
        public int? Companions { get; set; }

        // Position in this list becomes TripDestination.Visit_Order.
        public List<int> DestinationIds { get; set; } = new List<int>();
    }

    public class TripIdDto
    {
        public int TripId { get; set; }
    }

    public class TripSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = String.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Active";
        public decimal? Budget { get; set; }
        public int? Companions { get; set; }
        public int StopCount { get; set; }
    }

    public class TripDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = String.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Active";
        public decimal? Budget { get; set; }
        public int? Companions { get; set; }
        public int StopCount { get; set; }
        public List<TripStopDto> Stops { get; set; } = new List<TripStopDto>();
    }

    public class TripStopDto
    {
        public int StopId { get; set; }
        public int DestinationId { get; set; }
        public string Name { get; set; } = String.Empty;
        public string City { get; set; } = String.Empty;
        public string? Category { get; set; }
        public decimal? TicketPrice { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int VisitOrder { get; set; }
        public DateTime ArrivalDate { get; set; }
        public DateTime DepartureDate { get; set; }
    }
}
