using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class AdminSupportTicketRow
    {
        public SupportTicket Ticket { get; set; } = new();
        public string SubmitterName { get; set; } = "Unknown";
        public string SubmitterType { get; set; } = "Sponsor";
    }

    public class AdminSupportIndexVM
    {
        public List<AdminSupportTicketRow> Tickets { get; set; } = new();
        public string? StatusFilter { get; set; }
        public string? CategoryFilter { get; set; }
        public string? Search { get; set; }
        public string? SubmitterTypeFilter { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<string> Statuses { get; set; } = new() { "Open", "In Progress", "Resolved" };
        public List<string> SubmitterTypes { get; set; } = new() { "Sponsor", "Tourist" };
    }

    public class AdminSupportDetailsVM
    {
        public SupportTicket Ticket { get; set; } = new();
        public string SubmitterName { get; set; } = "Unknown";
        public string SubmitterType { get; set; } = "Sponsor";
        public string? RespondedByAdminName { get; set; }
        public List<string> Categories { get; set; } = new();
    }
}
