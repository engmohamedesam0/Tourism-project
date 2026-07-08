namespace Tourist_Project_MVC.Models
{
    // Join entity for the many-to-many between Reward and Branch: the same
    // reward can be offered at more than one branch.
    public class RewardBranch
    {
        public int RewardId { get; set; }
        public Reward? Reward { get; set; }

        public int BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
