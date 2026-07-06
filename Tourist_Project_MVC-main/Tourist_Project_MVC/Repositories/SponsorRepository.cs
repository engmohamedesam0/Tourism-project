using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class SponsorRepository : Repository<Sponsor>, ISponsorRepository
    {
        public SponsorRepository(TouristContext context) : base(context) { }

        public IEnumerable<Sponsor> GetFiltered(string? search, string? type)
        {
            var query = _context.Sponsors.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s =>
                    s.Name.Contains(search) ||
                    s.Address.Contains(search));

            if (!string.IsNullOrEmpty(type))
                query = query.Where(s => s.Type == type);

            return query.OrderBy(s => s.Name).ToList();
        }

        public Sponsor? GetByIdWithRewars(int id)
        {
            return _context.Sponsors
                .Include(s => s.Rewards)
                .FirstOrDefault(s => s.Id == id);
        }
    }
}