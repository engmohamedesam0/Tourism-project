using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class BadgeRepository : Repository<Badge>, IBadgeRepository
    {
        public BadgeRepository(TouristContext context) : base(context) { }

        public IEnumerable<Badge> GetBadgesForLevel(int currentXP, int currentLevel)
        {
            return _context.Badges
                .Where(b => b.XPRequired <= currentXP && b.LevelRequired <= currentLevel)
                .OrderBy(b => b.XPRequired)
                .ToList();
        }

        public Badge? GetByName(string name)
        {
            return _context.Badges
                .FirstOrDefault(b => b.Name == name);
        }
    }
}