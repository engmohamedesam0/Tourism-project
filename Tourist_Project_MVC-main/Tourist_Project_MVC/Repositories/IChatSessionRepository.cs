using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IChatSessionRepository : IRepository<ChatSession>
    {
        IEnumerable<ChatSession> GetByTouristId(int touristId);

        // Ownership filter by the authenticated user's email (server-side).
        // Case-insensitive to tolerate different casing between stored rows
        // and the identity claims. Newest first.
        IEnumerable<ChatSession> GetByUserEmail(string email);
    }
}
