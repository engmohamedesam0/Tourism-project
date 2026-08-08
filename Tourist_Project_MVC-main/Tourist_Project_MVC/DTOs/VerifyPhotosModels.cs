namespace Tourist_Project_MVC.DTOs
{
    // Models/VerifyPhotosModels.cs
    public class VerifyPhotosRequest
    {
        public List<string> Images { get; set; } = new();
    }

    public class PhotoVerificationResult
    {
        public int Index { get; set; }
        public bool Satisfies { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class VerifyPhotosResponse
    {
        public bool Verified { get; set; }
        public string? VerificationToken { get; set; }
        public string? VerificationPayload { get; set; }
        public List<PhotoVerificationResult> Results { get; set; } = new();
    }
}
