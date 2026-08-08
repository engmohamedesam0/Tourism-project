// DTOs/AuthDTOs.cs
namespace Tourist_Project_MVC.DTOs
{
    public class RegisterDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Country { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public int Level { get; set; }
        public string LevelLabel { get; set; }
        public int CurrentXP { get; set; }
        public int NextLevelXP { get; set; }
        public int PlacesVisited { get; set; }
        public int BadgesEarned { get; set; }
        public int LoginStreak { get; set; }
        public string FeaturedBadge { get; set; }
    }
}