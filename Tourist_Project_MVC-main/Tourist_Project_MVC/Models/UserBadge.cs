using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class UserBadge
    {
        public int Id { get; set; }

        public int TouristId { get; set; }

        [ForeignKey("TouristId")]
        public Tourist? Tourist { get; set; }

        public int BadgeId { get; set; }

        [ForeignKey("BadgeId")]
        public Badge? Badge { get; set; }

        public DateTime EarnedAt { get; set; } = DateTime.Now;

        public bool IsFeatured { get; set; }
    }
}