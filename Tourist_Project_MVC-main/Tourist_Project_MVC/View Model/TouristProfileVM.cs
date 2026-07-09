namespace Tourist_Project_MVC.View_Model
{
    public class TouristProfileVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Nationality { get; set; } = string.Empty;
        public DateTime RegisterDate { get; set; }
        public string? Status { get; set; }
        public int PointBalance { get; set; }
        public string? ProfilePicturePath { get; set; }
    }
}
