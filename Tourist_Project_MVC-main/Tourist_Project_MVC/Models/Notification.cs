namespace Tourist_Project_MVC.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int SponsorId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Optional link to the entity this notification is about, so a click can
        // route the sponsor to the relevant page (e.g. Reward edit, Redemption
        // history, or a Support ticket). Null for legacy/generic notifications.
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
    }
}
