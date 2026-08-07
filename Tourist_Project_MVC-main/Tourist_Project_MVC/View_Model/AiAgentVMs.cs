using System.Text.Json.Serialization;

namespace Tourist_Project_MVC.View_Model
{
    // ============================================================
    // Role-aware AI agent — request/response + tool argument models
    // ============================================================

    /// <summary>POST body for ConfirmPendingAction / CancelPendingAction.</summary>
    public class AiConfirmRequestVM
    {
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>GET /AiChat/StarterQuestions response — role is derived server-side.</summary>
    public class AiStarterQuestionsVM
    {
        public string Role { get; set; } = "Guest";
        public string Greeting { get; set; } = string.Empty;
        public List<string> Questions { get; set; } = new();
    }

    // ---- Shared / Guest tools ---------------------------------------------

    public class SearchDestinationsArgs
    {
        [JsonPropertyName("query")] public string? Query { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("limit")] public int? Limit { get; set; }
    }

    public class GetDestinationDetailsArgs
    {
        [JsonPropertyName("destination_id")] public int DestinationId { get; set; }
    }

    public class GetRecommendationsArgs
    {
        [JsonPropertyName("limit")] public int? Limit { get; set; }
    }

    // ---- Tourist tools -----------------------------------------------------

    public class CreateTripArgs
    {
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
        [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
        [JsonPropertyName("budget")] public decimal? Budget { get; set; }
        [JsonPropertyName("companions")] public int? Companions { get; set; }
        [JsonPropertyName("destination_ids")] public List<int> DestinationIds { get; set; } = new();
    }

    public class UpdateTripArgs
    {
        [JsonPropertyName("trip_id")] public int TripId { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("start_date")] public string? StartDate { get; set; }
        [JsonPropertyName("end_date")] public string? EndDate { get; set; }
        [JsonPropertyName("budget")] public decimal? Budget { get; set; }
        [JsonPropertyName("companions")] public int? Companions { get; set; }
        [JsonPropertyName("destination_ids")] public List<int>? DestinationIds { get; set; }
    }

    public class TripIdArgs
    {
        [JsonPropertyName("trip_id")] public int TripId { get; set; }
    }

    public class UpdateProfileArgs
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("nationality")] public string? Nationality { get; set; }
        [JsonPropertyName("preferred_language")] public string? PreferredLanguage { get; set; }
        [JsonPropertyName("travel_interests")] public string? TravelInterests { get; set; }
        [JsonPropertyName("notify_by_email")] public bool? NotifyByEmail { get; set; }
        [JsonPropertyName("notify_in_app")] public bool? NotifyInApp { get; set; }
    }

    // ---- Sponsor tools -----------------------------------------------------

    public class BranchDraftArgs
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("longitude")] public double? Longitude { get; set; }
        [JsonPropertyName("contact_number")] public int? ContactNumber { get; set; }
    }

    public class CreateBranchesArgs
    {
        [JsonPropertyName("branches")] public List<BranchDraftArgs> Branches { get; set; } = new();
    }

    public class UpdateBranchArgs
    {
        [JsonPropertyName("branch_id")] public int BranchId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("address")] public string? Address { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("longitude")] public double? Longitude { get; set; }
        [JsonPropertyName("contact_number")] public int? ContactNumber { get; set; }
    }

    public class BranchIdArgs
    {
        [JsonPropertyName("branch_id")] public int BranchId { get; set; }
    }

    public class SponsorRewardDraftArgs
    {
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("reward_type")] public string RewardType { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("points_required")] public int PointsRequired { get; set; }
        [JsonPropertyName("quantity_available")] public int? QuantityAvailable { get; set; }
        [JsonPropertyName("expiration_date")] public string ExpirationDate { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("branch_ids")] public List<int>? BranchIds { get; set; }
    }

    public class AdminRewardDraftArgs
    {
        [JsonPropertyName("sponsor_id")] public int SponsorId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("reward_type")] public string RewardType { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("points_required")] public int PointsRequired { get; set; }
        [JsonPropertyName("quantity_available")] public int? QuantityAvailable { get; set; }
        [JsonPropertyName("expiration_date")] public string ExpirationDate { get; set; } = string.Empty;
        [JsonPropertyName("status")] public string? Status { get; set; }
    }

    public class UpdateRewardArgs
    {
        [JsonPropertyName("reward_id")] public int RewardId { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("reward_type")] public string? RewardType { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("points_required")] public int? PointsRequired { get; set; }
        [JsonPropertyName("quantity_available")] public int? QuantityAvailable { get; set; }
        [JsonPropertyName("expiration_date")] public string? ExpirationDate { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
    }

    public class RewardIdArgs
    {
        [JsonPropertyName("reward_id")] public int RewardId { get; set; }
    }

    // ---- Admin tools -------------------------------------------------------

    public class AdminDestinationDraftArgs
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("arabic_name")] public string? ArabicName { get; set; }
        [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("tags")] public string? Tags { get; set; }
        [JsonPropertyName("ticket_required")] public string? TicketRequired { get; set; }
        [JsonPropertyName("egyptian_price")] public int? EgyptianPrice { get; set; }
        [JsonPropertyName("student_egyptian_price")] public int? StudentEgyptianPrice { get; set; }
        [JsonPropertyName("foreign_price")] public int? ForeignPrice { get; set; }
        [JsonPropertyName("student_foreign_price")] public int? StudentForeignPrice { get; set; }
        [JsonPropertyName("latitude")] public double? Latitude { get; set; }
        [JsonPropertyName("longitude")] public double? Longitude { get; set; }
        [JsonPropertyName("city_for_location")] public string? CityForLocation { get; set; }
        [JsonPropertyName("image_urls")] public List<string>? ImageUrls { get; set; }
    }

    public class UpdateDestinationArgs
    {
        [JsonPropertyName("destination_id")] public int DestinationId { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("arabic_name")] public string? ArabicName { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("tags")] public string? Tags { get; set; }
        [JsonPropertyName("egyptian_price")] public int? EgyptianPrice { get; set; }
        [JsonPropertyName("student_egyptian_price")] public int? StudentEgyptianPrice { get; set; }
        [JsonPropertyName("foreign_price")] public int? ForeignPrice { get; set; }
        [JsonPropertyName("student_foreign_price")] public int? StudentForeignPrice { get; set; }
        [JsonPropertyName("open_at")] public int? OpenAt { get; set; }
        [JsonPropertyName("close_at")] public int? CloseAt { get; set; }
    }

    public class DestinationIdArgs
    {
        [JsonPropertyName("destination_id")] public int DestinationId { get; set; }
    }

    public class GetUsersArgs
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
    }

    public class ChangeUserRoleArgs
    {
        [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
        [JsonPropertyName("new_role")] public string NewRole { get; set; } = string.Empty;
    }

    // ---- Meta (confirmation) tools ----------------------------------------

    public class PendingActionTokenArgs
    {
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    }

    // ---- Structured tool result payloads ----------------------------------

    public class AiTripActionData
    {
        public int? TripPlanId { get; set; }
        public string? TripPlanTitle { get; set; }
    }

    public class AiPhotoData
    {
        public List<string> PhotoUrls { get; set; } = new();
    }
}
