namespace Tourist_Project_MVC.DTOs
{
    public class MissionDTO
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public int Points { get; set; }
        public int MissDestId { get; set; }
    }
    public class CompleteMissionDto
    {
        public int MissionId { get; set; }
        public string? VerificationToken { get; set; }
        public string? VerificationPayload { get; set; }
    }
}
