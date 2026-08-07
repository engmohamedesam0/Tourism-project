namespace Tourist_Project_MVC.Models
{
    public class Notification
    {
        public int Id { get; set; }

        // Sponsor recipient (legacy rows + sponsor flow). Null for Admin/Tourist
        // notifications, which target the signed-in ApplicationUser instead.
        public int? SponsorId { get; set; }

        // Who the notification is for: "Sponsor" | "Admin" | "Tourist".
        // Null on legacy rows => treated as a Sponsor notification.
        public string? RecipientRole { get; set; }

        // ApplicationUser Id for Admin/Tourist recipients.
        // Null for Sponsor notifications and for role-wide Admin notifications
        // (every admin sees those).
        public string? RecipientUserId { get; set; }

        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Optional link to the entity this notification is about, so a click can
        // route the recipient to the relevant page (e.g. Reward edit, Redemption
        // history, a Support ticket, or a sponsor approval request). Null for
        // legacy/generic notifications.
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
    }
}
