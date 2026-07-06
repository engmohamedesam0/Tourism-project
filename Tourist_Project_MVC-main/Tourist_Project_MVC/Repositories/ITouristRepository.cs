using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ITouristRepository : IRepository<Tourist>
    {
        IEnumerable<Tourist> GetAllWithDetails();
        Tourist? GetByIdWithDetails(int id);
    }
}