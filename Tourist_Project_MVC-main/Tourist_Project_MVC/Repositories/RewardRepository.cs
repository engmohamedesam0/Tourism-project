using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class RewardRepository : Repository<Reward>, IRewardRepository
    {
        public RewardRepository(TouristContext context) : base(context) { }

        public IEnumerable<Reward> GetFiltered(string? search, string? rewardType)
        {
            var query = _context.Rewards
                .Include(r => r.Sponsor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(r =>
                    r.Title.Contains(search) ||
                    r.Description.Contains(search));

            if (!string.IsNullOrEmpty(rewardType))
                query = query.Where(r => r.RewardType == rewardType);

            return query.OrderBy(r => r.PointsRequired).ToList();
        }
    }
}