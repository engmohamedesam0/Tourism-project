namespace Tourist_Project_MVC.Models
{
    public class ChatSession
    {
        public int Id { get; set; }
        public int TouristId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string MessagesJson { get; set; } = "[]";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }
}
