namespace Tourist_Project_MVC.View_Model
{
    // One row of the unified admin account-management table.
    public class AccountRow
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
        public bool IsCurrentAdmin { get; set; }
    }
}
