using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services
{
    public interface IGamificationService
    {
        Task<(int XPAdded, List<Badge> NewBadges)> AwardXPAsync(int touristId, int xp, string source);
        Task<UserProgress> GetOrInitializeProgressAsync(int touristId);
        Task<List<UserBadge>> GetBadgesForTouristAsync(int touristId);
        Task<(int XPAdded, List<Badge> NewBadges)> AwardXPAndSaveAsync(int touristId, int xp, string source);
    }
}