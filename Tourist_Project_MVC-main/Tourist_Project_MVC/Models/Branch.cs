namespace Tourist_Project_MVC.Models
{
    // A physical location belonging to a Sponsor. A Sponsor may have several
    // branches, each with its own address and coordinates (moved here from the
    // single Lat/Long that used to live on Sponsor).
    public class Branch
    {
        public int Id { get; set; }

        public int SponsorId { get; set; }
        public Sponsor? Sponsor { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public float Lat { get; set; }
        public float Long { get; set; }
        public int? ContactNumber { get; set; }

        public List<RewardBranch>? RewardBranches { get; set; }
    }
}
