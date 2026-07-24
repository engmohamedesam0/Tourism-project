using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class ChatSessionRepository : Repository<ChatSession>, IChatSessionRepository
    {
        public ChatSessionRepository(TouristContext context) : base(context) { }

        public IEnumerable<ChatSession> GetByTouristId(int touristId)
        {
            return _context.ChatSessions
                .Where(s => s.TouristId == touristId)
                .OrderByDescending(s => s.UpdatedDate)
                .ToList();
        }
    }
}
