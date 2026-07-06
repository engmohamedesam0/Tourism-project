using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class TripPlan
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Active";

        [ForeignKey("TouristId")]
        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }

        public List<TripDestination> TripDestinations { get; set; } = new List<TripDestination>();
    }
}