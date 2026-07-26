using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Badge
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string Icon { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Rarity { get; set; } = "Common";

        public int XPRequired { get; set; }

        public int LevelRequired { get; set; }

        [MaxLength(50)]
        public string? RewardType { get; set; }

        [MaxLength(100)]
        public string? RewardValue { get; set; }

        public List<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
    }
}