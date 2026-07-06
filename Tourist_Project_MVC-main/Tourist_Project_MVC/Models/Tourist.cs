namespace Tourist_Project_MVC.Models
{
    public class Tourist
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? IdNumber { get; set; }
        public string? Passport { get; set; }
        public int point_Balance { get; set; }
        public DateTime RegisterDate { get; set; }
        public string? Status { get; set; } = "Active";

        // FK to the Identity login record (nullable: Tourists created by an Admin
        // via TouristController may not have a login account).
        public string? ApplicationUserId { get; set; }

        public List<TripPlan>? TripPlans { get; set; }
        public List<UserMission>? UserMissions { get; set; }
        public List<Redemption>? Redemptions { get; set; }
    }
}