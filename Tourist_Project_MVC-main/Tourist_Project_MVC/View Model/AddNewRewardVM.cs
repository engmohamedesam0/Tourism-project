using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class AddNewRewardVM
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string RewardType { get; set; }
        public string Description { get; set; }
        public int PointsRequired { get; set; }
        public int QuantityAvailable { get; set; }
        [Display(Name = "Expiration Date")]
        public DateTime ExpirationDate { get; set; }

        public int SponsorId { get; set; }
        [Display(Name = "Sponsor")]
        public IEnumerable<Sponsor>? Sponsors { get; set; }

    }
}
