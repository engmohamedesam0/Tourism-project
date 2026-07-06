using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ISponsorRepository : IRepository<Sponsor>
    {
        IEnumerable<Sponsor> GetFiltered(string? search, string? type);
        Sponsor? GetByIdWithRewars(int id);
    }
}