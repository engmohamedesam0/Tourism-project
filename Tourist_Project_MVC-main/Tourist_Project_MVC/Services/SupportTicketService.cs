using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services
{
    public interface ISupportTicketService
    {
        void Create(SupportTicket ticket);
        List<SupportTicket> GetBySponsorId(int sponsorId);
        SupportTicket? GetById(int id, int sponsorId);
    }

    public class SupportTicketService : ISupportTicketService
    {
        private readonly TouristContext _context;

        public SupportTicketService(TouristContext context)
        {
            _context = context;
        }

        public void Create(SupportTicket ticket)
        {
            ticket.Status = "Open";
            ticket.CreatedDate = DateTime.Now;
            _context.SupportTickets.Add(ticket);
            _context.SaveChanges();
        }

        public List<SupportTicket> GetBySponsorId(int sponsorId)
        {
            return _context.SupportTickets
                .Where(st => st.SponsorId == sponsorId)
                .OrderByDescending(st => st.CreatedDate)
                .ToList();
        }

        public SupportTicket? GetById(int id, int sponsorId)
        {
            return _context.SupportTickets
                .FirstOrDefault(st => st.Id == id && st.SponsorId == sponsorId);
        }
    }
}
