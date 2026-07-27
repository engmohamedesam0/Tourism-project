namespace Tourist_Project_MVC.DTOs
{
    public class RewardDTO
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public int Points { get; set; }
        public int Quntity { get; set; }
        public DateTime Expiration { get; set; }
        public string Status { get; set; }
    }
}
