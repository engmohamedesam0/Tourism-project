namespace Tourist_Project_MVC.View_Model
{
    public class AdminNavBadgesVM
    {
        public int PendingApprovalsCount { get; set; }
        public int UnresolvedSupportCount { get; set; }
        public int TotalCount => PendingApprovalsCount + UnresolvedSupportCount;
    }
}
