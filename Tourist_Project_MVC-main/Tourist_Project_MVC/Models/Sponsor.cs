namespace Tourist_Project_MVC.Models
{
    public class Sponsor
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int ContactNumber { get; set; }
        public List<Reward>? Rewards { get; set; }

    }
}
