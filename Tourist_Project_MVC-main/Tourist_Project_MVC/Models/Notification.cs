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
    }
}
