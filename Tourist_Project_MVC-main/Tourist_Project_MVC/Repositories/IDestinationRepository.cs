using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IDestinationRepository : IRepository<Destination>
    {
        IEnumerable<Destination> GetFiltered(string? search, string? status, string? category);
    }
}