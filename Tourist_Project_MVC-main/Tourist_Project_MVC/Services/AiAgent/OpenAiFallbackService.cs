using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// OpenAI chat-completions fallback used ONLY when the primary provider
    /// (Gemini) reports quota/credit exhaustion (HTTP 429 / RESOURCE_EXHAUSTED).
    ///
    /// The fallback reuses the EXACT SAME context the Gemini request would have
    /// received: the same system prompt (identity block + role context block +
    /// destinations block) and the same conversation contents are forwarded
    /// as-is. Nothing is re-generated — no second knowledge source, no new
    /// embeddings, no retrieval, no vector store. The existing context remains
    /// the single source of knowledge.
    ///
    /// Deliberately single-shot and text-only: the fallback never executes
    /// tools, so no state can be changed while the primary provider is
    /// degraded. Returns null when OpenAI is not configured or the call fails,
    /// letting the caller fall back to its existing error handling.
    /// </summary>
    public interface IOpenAiFallbackService
    {
        /// <summary>
        /// Sends the given system prompt + conversation contents to OpenAI.
        /// Returns the assistant text reply, or null if OpenAI is not
        /// configured or the request failed.
        /// </summary>
        Task<string?> TryGetTextReplyAsync(string systemPrompt, IReadOnlyList<GeminiContent> contents, CancellationToken ct = default);
    }

    public class OpenAiFallbackService : IOpenAiFallbackService
    {
        private const string Endpoint = "https://api.openai.com/v1/chat/completions";

        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly ILogger<OpenAiFallbackService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public OpenAiFallbackService(HttpClient http, IConfiguration config, ILogger<OpenAiFallbackService> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        private string ApiKey => _config["OpenAI:ApiKey"] ?? string.Empty;
        private string Model => _config["OpenAI:Model"] ?? "gpt-4o-mini";

        public async Task<string?> TryGetTextReplyAsync(string systemPrompt, IReadOnlyList<GeminiContent> contents, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _logger.LogDebug("OpenAI fallback skipped: OpenAI:ApiKey is not configured.");
                return null;
            }

            try
            {
                var messages = BuildMessages(systemPrompt, contents);
                if (messages.Count == 0)
                    return null;

                var payload = new OpenAiChatRequest
                {
                    Model = Model,
                    Messages = messages,
                    Temperature = 0.4
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                httpRequest.Headers.Add("Authorization", $"Bearer {ApiKey}");
                httpRequest.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                using var httpResponse = await _http.SendAsync(httpRequest, ct);
                var body = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI fallback API error {Status}: {Body}", httpResponse.StatusCode, body);
                    return null;
                }

                var response = JsonSerializer.Deserialize<OpenAiChatResponse>(body, _jsonOptions);
                var reply = response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                return string.IsNullOrWhiteSpace(reply) ? null : reply;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling OpenAI fallback.");
                return null;
            }
        }

        /// <summary>
        /// Maps the existing Gemini conversation to OpenAI messages. Text parts
        /// become text content; image parts (base64, image/*) become data-URL
        /// image content. Function-call / function-response parts are skipped —
        /// the fallback never executes tools.
        /// </summary>
        private static List<OpenAiMessage> BuildMessages(string systemPrompt, IReadOnlyList<GeminiContent> contents)
        {
            var messages = new List<OpenAiMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            foreach (var content in contents)
            {
                var role = string.Equals(content.Role, "model", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";

                var text = string.Concat(content.Parts
                    .Where(p => p.Text != null)
                    .Select(p => p.Text));

                var images = content.Parts
                    .Where(p => p.InlineData != null && IsImageMime(p.InlineData.MimeType))
                    .Select(p => p.InlineData!)
                    .ToList();

                if (string.IsNullOrWhiteSpace(text) && images.Count == 0)
                    continue;

                if (images.Count == 0)
                {
                    messages.Add(new OpenAiMessage { Role = role, Content = text });
                    continue;
                }

                // Multi-part message: text + one or more images.
                var parts = new List<OpenAiContentPart>();
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(new OpenAiContentPart { Type = "text", Text = text });

                foreach (var image in images)
                {
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl { Url = $"data:{image.MimeType};base64,{image.Data}" }
                    });
                }

                messages.Add(new OpenAiMessage { Role = role, Content = parts });
            }

            return messages;
        }

        private static bool IsImageMime(string mimeType)
        {
            return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        // ---- OpenAI chat completions wire format (only the fields we use) ----
        // Docs: https://platform.openai.com/docs/api-reference/chat

        private sealed class OpenAiChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OpenAiMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; } = 0.4;
        }

        private sealed class OpenAiMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            // Either a plain string or a List<OpenAiContentPart> (multi-modal).
            [JsonPropertyName("content")]
            public object Content { get; set; } = string.Empty;
        }

        private sealed class OpenAiContentPart
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string? Text { get; set; }

            [JsonPropertyName("image_url")]
            public OpenAiImageUrl? ImageUrl { get; set; }
        }

        private sealed class OpenAiImageUrl
        {
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }

        private sealed class OpenAiChatResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAiChoice>? Choices { get; set; }
        }

        private sealed class OpenAiChoice
        {
            [JsonPropertyName("message")]
            public OpenAiResponseMessage? Message { get; set; }
        }

        private sealed class OpenAiResponseMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
