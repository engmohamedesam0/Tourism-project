using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class DestinationRepository : Repository<Destination>, IDestinationRepository
    {
        public DestinationRepository(TouristContext context) : base(context) { }

        public IEnumerable<Destination> GetFiltered(string? search, string? status, string? category)
        {
            var query = _context.Destinations.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(d =>
                    d.Name.Contains(search) ||
                    d.City.Contains(search));

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status == status);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(d => d.Category == category);

            return query.OrderByDescending(d => d.Visits).ToList();
        }
    }
}