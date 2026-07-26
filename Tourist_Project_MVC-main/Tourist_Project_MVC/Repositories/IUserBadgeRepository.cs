using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IUserBadgeRepository : IRepository<UserBadge>
    {
        List<UserBadge> GetByTouristId(int touristId);
        bool HasBadge(int touristId, int badgeId);
        List<UserBadge> GetUnearnedByTourist(int touristId);
    }
}