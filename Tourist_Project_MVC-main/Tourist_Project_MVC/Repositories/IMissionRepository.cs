using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IMissionRepository : IRepository<Mission>
    {
        IEnumerable<Mission> GetFiltered(string? search, string? missionType);
    }
}