namespace Tourist_Project_MVC.Models
{
    public class SupportTicket
    {
        public int Id { get; set; }
        public int? SponsorId { get; set; }

        // Nullable FK for tourist-submitted tickets. Exactly one of SponsorId /
        // TouristId must be set (enforced in service/controller), never both.
        public int? TouristId { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        public string? AttachmentPath { get; set; }
        public string Status { get; set; } = "Open";
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string? AdminResponse { get; set; }
        public DateTime? RespondedDate { get; set; }
        public string? RespondedByAdminId { get; set; }

        public string? SponsorResponse { get; set; }
        public DateTime? SponsorRespondedDate { get; set; }
    }
}
