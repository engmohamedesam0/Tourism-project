using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services
{
    public interface ISupportTicketService
    {
        void Create(SupportTicket ticket);
        List<SupportTicket> GetBySponsorId(int sponsorId);
        SupportTicket? GetById(int id, int sponsorId);
        SupportTicket? GetByIdForAdmin(int id);
        List<SupportTicket> GetByTouristId(int touristId);
        SupportTicket? GetByIdForTourist(int id, int touristId);
        List<SupportTicket> GetAll();
        void Update(SupportTicket ticket);
        List<SupportTicket> GetTouristTicketsForSponsor(int sponsorId);
        SupportTicket? GetTouristTicketForSponsor(int id, int sponsorId);
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
                .Where(st => st.SponsorId == sponsorId && !st.TouristId.HasValue)
                .OrderByDescending(st => st.CreatedDate)
                .ToList();
        }

        public SupportTicket? GetById(int id, int sponsorId)
        {
            return _context.SupportTickets
                .FirstOrDefault(st => st.Id == id && st.SponsorId == sponsorId && !st.TouristId.HasValue);
        }

        public SupportTicket? GetByIdForAdmin(int id)
        {
            return _context.SupportTickets
                .FirstOrDefault(st => st.Id == id);
        }

        public List<SupportTicket> GetByTouristId(int touristId)
        {
            return _context.SupportTickets
                .Where(st => st.TouristId == touristId)
                .OrderByDescending(st => st.CreatedDate)
                .ToList();
        }

        public SupportTicket? GetByIdForTourist(int id, int touristId)
        {
            return _context.SupportTickets
                .FirstOrDefault(st => st.Id == id && st.TouristId == touristId);
        }

        public List<SupportTicket> GetAll()
        {
            return _context.SupportTickets
                .OrderByDescending(st => st.CreatedDate)
                .ToList();
        }

        public void Update(SupportTicket ticket)
        {
            _context.SupportTickets.Update(ticket);
            _context.SaveChanges();
        }

        public List<SupportTicket> GetTouristTicketsForSponsor(int sponsorId)
        {
            return _context.SupportTickets
                .Where(st => st.SponsorId == sponsorId && st.TouristId.HasValue)
                .OrderByDescending(st => st.CreatedDate)
                .ToList();
        }

        public SupportTicket? GetTouristTicketForSponsor(int id, int sponsorId)
        {
            return _context.SupportTickets
                .FirstOrDefault(st => st.Id == id && st.SponsorId == sponsorId && st.TouristId.HasValue);
        }
    }
}
