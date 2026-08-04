namespace Tourist_Project_MVC.Models
{
    public class ChatSession
    {
        public int Id { get; set; }
        public int TouristId { get; set; }

        // Ownership by email — set from the server-side authenticated identity
        // (ApplicationUser.Email), never from frontend input. Queries filter on
        // this column so each user only ever sees their own conversations.
        public string? UserEmail { get; set; }

        public string Title { get; set; } = string.Empty;
        public string MessagesJson { get; set; } = "[]";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime UpdatedDate { get; set; } = DateTime.Now;
    }
}
