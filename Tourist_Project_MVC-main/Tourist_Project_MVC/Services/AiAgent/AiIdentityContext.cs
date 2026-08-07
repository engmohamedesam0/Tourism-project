using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// Immutable snapshot of the CURRENT request's authentication state, resolved
    /// strictly server-side from the ASP.NET Core Identity / JWT identity. The AI
    /// model and the client never supply any of these values — if they try, they
    /// are ignored. This is the single source of truth every AI tool must use for
    /// "who am I" and "what do I own".
    /// </summary>
    public class AiIdentityContext
    {
        public const string RoleGuest = "Guest";
        public const string RoleTourist = "User";      // Identity role name for tourists
        public const string RoleSponsor = "Sponsor";
        public const string RoleAdmin = "Admin";

        /// <summary>Identity role name: Guest | User | Sponsor | Admin.</summary>
        public string Role { get; init; } = RoleGuest;

        public bool IsAuthenticated => Role != RoleGuest;

        /// <summary>The authenticated ApplicationUser, or null for guests.</summary>
        public ApplicationUser? User { get; init; }

        /// <summary>
        /// The Tourist record linked to the authenticated user. Populated ONLY for
        /// the "User" role. Never null for tourists (auto-created like every other
        /// controller does via ITouristRepository.GetOrCreateByApplicationUser).
        /// </summary>
        public Tourist? Tourist { get; init; }

        /// <summary>
        /// The Sponsor record linked to the authenticated user. Populated ONLY for
        /// the "Sponsor" role.
        /// </summary>
        public Sponsor? Sponsor { get; init; }

        public string? UserId => User?.Id;
        public string? Email => User?.Email;
    }
}
