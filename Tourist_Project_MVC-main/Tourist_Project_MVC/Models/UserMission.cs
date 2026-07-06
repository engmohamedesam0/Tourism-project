using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class UserMission
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public int PointsEarned { get; set; }
        public DateTime Completed_At { get; set; }

        [ForeignKey("TouristId")]
        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }
        [ForeignKey("MissionId")]
        public int MissionId { get; set; }
        public Mission? Mission { get; set; }
    }
}
