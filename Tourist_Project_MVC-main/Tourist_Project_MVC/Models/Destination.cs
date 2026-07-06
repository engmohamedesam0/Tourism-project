using System.ComponentModel.DataAnnotations;

namespace Tourist_Project_MVC.Models
{
    public class Destination
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public DateTime? OpeningHours { get; set; }
        public string? Category { get; set; }
        public float Lat { get; set; }
        public float Long { get; set; }
        public string? Description { get; set; }
        public decimal? TicketPrice { get; set; }

        public int Visits { get; set; } = 0;
        public string Status { get; set; } = "Active";

        public List<Mission> Missions { get; set; } = new List<Mission>();
        public List<TripDestination> TripDestinations { get; set; } = new List<TripDestination>();
    }
}