using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [ForeignKey("SponsorId")]
        public int SponsorId { get; set; }
        public Sponsor? Sponsor { get; set; }

        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10, 2)")]
        public decimal Price { get; set; }

        public string? Description { get; set; }
    }
}
