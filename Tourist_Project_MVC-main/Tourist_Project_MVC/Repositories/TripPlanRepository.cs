using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class TripPlanRepository : Repository<TripPlan>, ITripPlanRepository
    {
        public TripPlanRepository(TouristContext context) : base(context) { }

        public IEnumerable<TripPlan> GetAllWithDetails()
        {
            return _context.TripPlans
                .Include(t => t.Tourist)
                .Include(t => t.TripDestinations)
                    .ThenInclude(td => td.Destination)
                .ToList();
        }

        public TripPlan? GetByIdWithDetails(int id)
        {
            return _context.TripPlans
                .Include(t => t.Tourist)
                .Include(t => t.TripDestinations)
                    .ThenInclude(td => td.Destination)
                .FirstOrDefault(t => t.Id == id);
        }

        public void RemoveTripDestinations(int tripPlanId)
        {
            var existing = _context.TripDestinations
                .Where(td => td.TripPlanId == tripPlanId)
                .ToList();
            _context.TripDestinations.RemoveRange(existing);
        }

        public IEnumerable<TripPlan> GetFiltered(string? search, int? touristId)
        {
            var query = _context.TripPlans
                .Include(t => t.Tourist)
                .Include(t => t.TripDestinations)
                    .ThenInclude(td => td.Destination)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.Title.Contains(search));

            if (touristId.HasValue)
                query = query.Where(t => t.TouristId == touristId);

            return query.OrderByDescending(t => t.StartDate).ToList();
        }
    }
}