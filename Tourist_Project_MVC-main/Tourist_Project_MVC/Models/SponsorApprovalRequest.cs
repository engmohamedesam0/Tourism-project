namespace Tourist_Project_MVC.Models
{
    public class SponsorApprovalRequest
    {
        public int Id { get; set; }

        // FK to the Identity login record that initiated the sponsor sign-up.
        public string ApplicationUserId { get; set; } = string.Empty;

        // "Pending" | "Approved" | "Rejected"
        public string Status { get; set; } = "Pending";

        public DateTime RequestedDate { get; set; } = DateTime.Now;

        public DateTime? ReviewedDate { get; set; }

        // FK to the Admin (ApplicationUser) who approved/rejected the request.
        public string? ReviewedByAdminId { get; set; }
    }
}
