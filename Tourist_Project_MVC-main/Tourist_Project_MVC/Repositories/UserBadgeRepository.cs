using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class UserBadgeRepository : Repository<UserBadge>, IUserBadgeRepository
    {
        public UserBadgeRepository(TouristContext context) : base(context) { }

        public List<UserBadge> GetByTouristId(int touristId)
        {
            return _context.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.TouristId == touristId)
                .OrderByDescending(ub => ub.EarnedAt)
                .ToList();
        }

        public bool HasBadge(int touristId, int badgeId)
        {
            return _context.UserBadges
                .Any(ub => ub.TouristId == touristId && ub.BadgeId == badgeId);
        }

        public List<UserBadge> GetUnearnedByTourist(int touristId)
        {
            var earnedBadgeIds = _context.UserBadges
                .Where(ub => ub.TouristId == touristId)
                .Select(ub => ub.BadgeId)
                .ToList();

            return _context.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.TouristId == touristId && !earnedBadgeIds.Contains(ub.BadgeId))
                .ToList();
        }
    }
}