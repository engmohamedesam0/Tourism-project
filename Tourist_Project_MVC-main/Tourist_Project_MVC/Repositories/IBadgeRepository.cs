using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IBadgeRepository : IRepository<Badge>
    {
        IEnumerable<Badge> GetBadgesForLevel(int currentXP, int currentLevel);
        Badge? GetByName(string name);
    }
}