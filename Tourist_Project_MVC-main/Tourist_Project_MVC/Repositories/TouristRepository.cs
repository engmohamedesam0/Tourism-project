using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class TouristRepository : Repository<Tourist>, ITouristRepository
    {
        public TouristRepository(TouristContext context) : base(context) { }

        public IEnumerable<Tourist> GetAllWithDetails()
        {
            return _context.Tourists
                .Include(t => t.TripPlans)
                .Include(t => t.UserMissions)
                .ToList();
        }

        public Tourist? GetByIdWithDetails(int id)
        {
            return _context.Tourists
                .Include(t => t.TripPlans)
                .Include(t => t.UserMissions)
                    .ThenInclude(um => um.Mission)
                .Include(t => t.Redemptions)
                .FirstOrDefault(t => t.Id == id);
        }
    }
}