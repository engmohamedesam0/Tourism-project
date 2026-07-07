using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Review
    {
        public int Id { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }

        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }

        [ForeignKey("SponsorId")]
        public int SponsorId { get; set; }
        public Sponsor? Sponsor { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
