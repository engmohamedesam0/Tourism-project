namespace Tourist_Project_MVC.View_Model
{
    // What the mobile app posts to /api/auth/login.
    public class MobileLoginRequestVM
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // What /api/auth/login returns on success. The mobile app stores Token
    // and sends it as "Authorization: Bearer {Token}" on every later request.
    public class MobileLoginResponseVM
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
