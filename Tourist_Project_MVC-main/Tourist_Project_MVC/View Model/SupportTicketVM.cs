using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class SupportTicketVM
    {
        public List<SupportTicket> Tickets { get; set; } = new();
        public string? Subject { get; set; }
        public string? Description { get; set; }
    }
}
