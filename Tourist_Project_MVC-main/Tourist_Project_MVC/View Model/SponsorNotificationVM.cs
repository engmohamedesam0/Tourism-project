using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class SponsorNotificationVM
    {
        public List<Notification> Notifications { get; set; } = new();
        public List<SupportTicket> SupportTickets { get; set; } = new();
        public string? Filter { get; set; }
    }
}
