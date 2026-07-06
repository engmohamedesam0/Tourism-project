using Microsoft.AspNetCore.Identity;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ITouristRepository : IRepository<Tourist>
    {
        IEnumerable<Tourist> GetAllWithDetails();
        Tourist? GetByIdWithDetails(int id);

        // Resolves (and self-heals) the Tourist linked to a signed-in Identity user.
        // Returns a usable Tourist for any authenticated "User" — never null.
        Tourist GetOrCreateByApplicationUser(ApplicationUser user);
    }
}