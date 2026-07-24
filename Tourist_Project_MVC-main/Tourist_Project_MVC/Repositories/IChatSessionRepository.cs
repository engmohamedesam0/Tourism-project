using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IChatSessionRepository : IRepository<ChatSession>
    {
        IEnumerable<ChatSession> GetByTouristId(int touristId);
    }
}
