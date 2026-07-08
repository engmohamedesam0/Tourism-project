using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class SponsorRewardVM
    {
        public int Id { get; set; }

        [Display(Name = "Title")]
        [Required(ErrorMessage = "This Field required")]
        public string Title { get; set; }

        [Display(Name = "Type")]
        [Required(ErrorMessage = "This Field required")]
        public string RewardType { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Points Required")]
        [Range(1, int.MaxValue, ErrorMessage = "Points must be at least 1")]
        public int PointsRequired { get; set; }

        [Display(Name = "Quantity Available")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
        public int QuantityAvailable { get; set; }

        [Display(Name = "Expiration Date")]
        public DateTime ExpirationDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Active";

        [Display(Name = "Available Branches")]
        public List<int> SelectedBranchIds { get; set; } = new();

        public List<Branch>? AvailableBranches { get; set; }

        public int SponsorId { get; set; }
    }
}
