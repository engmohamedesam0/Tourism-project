using System.ComponentModel.DataAnnotations.Schema;

namespace Tourist_Project_MVC.Models
{
    public class Tourist
    {
        public int Id { get; set; }

        // Tourist-specific fields only. Shared identity/profile data
        // (name, email, nationality, phone, photo) lives on ApplicationUser
        // and is exposed here as read-only computed properties so the rest of
        // the codebase can keep using tourist.Name / .Email / .Nationality
        // without duplicating columns in the database.
        public string? IdNumber { get; set; }
        public string? Passport { get; set; }
        public int point_Balance { get; set; }
        public DateTime RegisterDate { get; set; }
        public string? Status { get; set; } = "Active";

        public string? PreferredLanguage { get; set; }
        public string? TravelInterests { get; set; }
        public bool NotifyByEmail { get; set; } = true;
        public bool NotifyInApp { get; set; } = true;

        // FK to the Identity login record (nullable: Tourists created by an Admin
        // via TouristController may not have a login account).
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        // --- Computed read-only profile properties (not mapped to columns) ---
        [NotMapped]
        public string Name => ApplicationUser != null
            ? $"{ApplicationUser.FirstName} {ApplicationUser.LastName}".Trim()
            : "Unknown";

        [NotMapped]
        public string Email => ApplicationUser?.Email ?? string.Empty;

        [NotMapped]
        public string Nationality => ApplicationUser?.Nationality ?? string.Empty;

        public List<TripPlan>? TripPlans { get; set; }
        public List<UserMission>? UserMissions { get; set; }
        public List<Redemption>? Redemptions { get; set; }
        public UserProgress? UserProgress { get; set; }
        public List<UserBadge>? UserBadges { get; set; }
    }
}