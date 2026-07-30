namespace Tourist_Project_MVC.DTOs
{
    public class DestinationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public string? Category { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Active";
        public decimal? Rating { get; set; }
    }
    public class DestinationIdDto
    {
        public int DestinationId { get; set; }
    }
    public class DestinationDetailsDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
        public string City { get; set; } = String.Empty;
        public DateTime? OpenHour { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public string? Description { get; set; }
        public string? Image { get; set; }
        public decimal? TicketPrice { get; set; }
        public string Status { get; set; } = "Active";
        public decimal? Rating { get; set; }
        public int Visitors { get; set; }
    }
}
