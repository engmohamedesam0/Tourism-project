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

        public IEnumerable<Reward> GetBySponsorId(int sponsorId)
        {
            return _context.Rewards
                .Include(r => r.Sponsor)
                .Include(r => r.RewardBranches)
                .ThenInclude(rb => rb.Branch)
                .Where(r => r.SponsorId == sponsorId)
                .OrderBy(r => r.PointsRequired)
                .ToList();
        }

        public Reward? GetByIdWithBranches(int id)
        {
            return _context.Rewards
                .Include(r => r.Sponsor)
                .Include(r => r.RewardBranches)
                .ThenInclude(rb => rb.Branch)
                .FirstOrDefault(r => r.Id == id);
        }
    }
}