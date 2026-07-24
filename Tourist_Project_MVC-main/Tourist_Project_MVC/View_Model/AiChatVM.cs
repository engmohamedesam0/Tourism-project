using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tourist_Project_MVC.View_Model
{
    // One turn in the conversation, as sent back and forth between the
    // browser and the server. "Role" is either "user" or "assistant".
    public class AiChatMessageVM
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    // What the browser posts to /AiChat/Send.
    public class AiChatRequestVM
    {
        public string Message { get; set; } = string.Empty;

        // Arrives as a JSON-serialized string when sent via multipart/form-data.
        public string History { get; set; } = "[]";

        // Images sent as JSON array of base64 strings.
        public string ImagesBase64 { get; set; } = "[]";

        // Corresponding MIME types for each image (JSON array).
        public string ImagesMimeTypes { get; set; } = "[]";

        // Audio sent as a single base64 string.
        public string? AudioBase64 { get; set; }
        public string? AudioMimeType { get; set; }
    }

    // What the server returns to the browser.
    public class AiChatResponseVM
    {
        public string Reply { get; set; } = string.Empty;

        // Populated only when the assistant actually created/updated a trip plan.
        public bool TripSaved { get; set; }
        public int? TripPlanId { get; set; }
        public string? TripPlanTitle { get; set; }
    }

    // Compact projection of a Destination, cheap enough to inline into
    // every system prompt so the model only ever proposes real, bookable
    // destinations (and knows their real IDs, prices, and cities).
    public class AiDestinationContext
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal? TicketPrice { get; set; }
        public decimal? Rating { get; set; }
    }

    // ---- Gemini generateContent wire format (only the fields we use) ----
    // Docs: https://ai.google.dev/api/generate-content

    public class GeminiRequest
    {
        [JsonPropertyName("system_instruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("tools")]
        public List<GeminiTool>? Tools { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    public class GeminiContent
    {
        // "user" or "model" (Gemini has no "system"/"assistant" roles in contents;
        // system prompt goes in the separate SystemInstruction field instead).
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("inlineData")]
        public GeminiInlineData? InlineData { get; set; }

        [JsonPropertyName("functionCall")]
        public GeminiFunctionCall? FunctionCall { get; set; }
    }

    public class GeminiInlineData
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }

    public class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    public class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
    }

    public class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public object Parameters { get; set; } = new();
    }

    public class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.4;
    }

    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public GeminiPromptFeedback? PromptFeedback { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    public class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }

    // Arguments shape for the "save_trip_plan" tool call — matches the
    // JSON schema declared in AiChatService.BuildSaveTripTool().
    public class SaveTripArgs
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "My Trip";

        [JsonPropertyName("start_date")]
        public string StartDate { get; set; } = string.Empty;

        [JsonPropertyName("end_date")]
        public string EndDate { get; set; } = string.Empty;

        [JsonPropertyName("budget")]
        public decimal? Budget { get; set; }

        [JsonPropertyName("companions")]
        public int? Companions { get; set; }

        [JsonPropertyName("destination_ids")]
        public List<int> DestinationIds { get; set; } = new();
    }
}
