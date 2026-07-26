using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IUserProgressRepository : IRepository<UserProgress>
    {
        UserProgress? GetByTouristId(int touristId);
        Task<UserProgress> GetOrCreateForTouristAsync(int touristId);
    }
}