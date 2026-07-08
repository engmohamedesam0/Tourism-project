using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class BranchRepository : Repository<Branch>, IBranchRepository
    {
        public BranchRepository(TouristContext context) : base(context) { }

        public IEnumerable<Branch> GetBySponsorId(int sponsorId)
        {
            return _context.Branches
                .Where(b => b.SponsorId == sponsorId)
                .ToList();
        }
    }
}
