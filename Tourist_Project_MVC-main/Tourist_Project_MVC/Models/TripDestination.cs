using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class TripDestination
    {
        public int Id { get; set; }
        public int Visit_Order { get; set; }
        public DateTime ArrivalDate { get; set; }
        public DateTime DepartureDate { get; set; }

        [ForeignKey("TripPlanId")]
        public int TripPlanId { get; set; }
        public TripPlan? TripPlan { get; set; }
        [ForeignKey("DestinationId")]
        public int DestinationId { get; set; }
        public Destination? Destination { get; set; }
    }
}
