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

        // Single source of truth for "who is the current tourist".
        // 1) by ApplicationUserId, 2) by email (and link), 3) auto-create.
        public Tourist GetOrCreateByApplicationUser(ApplicationUser user)
        {
            if (user != null && !string.IsNullOrWhiteSpace(user.Id))
            {
                var byId = _context.Tourists
                    .FirstOrDefault(t => t.ApplicationUserId == user.Id);
                if (byId != null)
                    return byId;
            }

            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                var email = user.Email.ToLower();
                var byEmail = _context.Tourists
                    .FirstOrDefault(t => t.Email != null && t.Email.ToLower() == email);
                if (byEmail != null)
                {
                    // Self-heal: persist the link so it only happens once.
                    byEmail.ApplicationUserId = user.Id;
                    Update(byEmail);
                    Save();
                    return byEmail;
                }
            }

            var created = new Tourist
            {
                Name = user?.UserName ?? user?.Email ?? "Tourist",
                Email = user?.Email ?? string.Empty,
                Nationality = string.Empty,
                Password = string.Empty,
                RegisterDate = DateTime.Now,
                Status = "Active",
                point_Balance = 0,
                ApplicationUserId = user?.Id
            };

            Add(created);
            Save();
            return created;
        }
    }
}