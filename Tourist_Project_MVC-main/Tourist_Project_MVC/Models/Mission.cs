using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Mission
    {
        public int Id { get; set; }
        public string MissionType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PointsReward { get; set; }

        [ForeignKey("DestinationId")]
        public int DestinationId { get; set; }
        public Destination? Destination { get; set; }

        public List<UserMission>? UserMissions { get; set; }
    }
}