namespace Tourist_Project_MVC.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public int SponsorId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
