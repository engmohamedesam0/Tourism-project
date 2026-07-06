using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class MissionWithDeptListVM
    {
        public int Id { get; set; }
        [Display(Name = "Mission Type")]
        public string MissionType { get; set; }
        public string Title { get; set; }
        public string ?Description { get; set; }
        [Display(Name = "Points")]
        [Range(0, int.MaxValue)]
        public int PointsReward { get; set; }

        public int DestinationId { get; set; }
        public List<Destination> ?destinations { get; set; }
    }
}
