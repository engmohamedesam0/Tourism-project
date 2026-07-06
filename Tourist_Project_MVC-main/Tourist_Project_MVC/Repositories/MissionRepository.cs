using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class MissionRepository : Repository<Mission>, IMissionRepository
    {
        public MissionRepository(TouristContext context) : base(context) { }

        public IEnumerable<Mission> GetFiltered(string? search, string? missionType)
        {
            var query = _context.Missions
                .Include(m => m.Destination)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(m =>
                    m.Title.Contains(search) ||
                    m.Description.Contains(search));

            if (!string.IsNullOrEmpty(missionType))
                query = query.Where(m => m.MissionType == missionType);

            return query.OrderByDescending(m => m.PointsReward).ToList();
        }
    }
}