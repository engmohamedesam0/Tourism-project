using System.Text.Json;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Services
{
    /// <summary>
    /// Persists chat turns into ChatSession rows for authenticated tourists.
    /// Sponsors and Admins do not get persisted history (ChatSession.TouristId
    /// is a non-nullable FK and we deliberately avoid a schema change) — their
    /// conversations simply stay ephemeral, like guest chats.
    /// </summary>
    public interface IChatHistoryService
    {
        /// <summary>Persists the turn (user + assistant) and stamps response.ChatSessionId. No-op for non-tourists.</summary>
        Task PersistTurnAsync(AiChatRequestVM request, AiChatResponseVM response, AiIdentityContext identity);
    }

    public class ChatHistoryService : IChatHistoryService
    {
        private readonly IChatSessionRepository _chatSessionRepo;
        private readonly ILogger<ChatHistoryService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ChatHistoryService(IChatSessionRepository chatSessionRepo, ILogger<ChatHistoryService> logger)
        {
            _chatSessionRepo = chatSessionRepo;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task PersistTurnAsync(AiChatRequestVM request, AiChatResponseVM response, AiIdentityContext identity)
        {
            var tourist = identity.Tourist;
            var userEmail = identity.Email;
            if (tourist == null) return;

            try
            {
                ChatSession? session = null;

                if (request.ChatSessionId.HasValue)
                {
                    session = await _chatSessionRepo.GetByIdAsync(request.ChatSessionId.Value);
                    // Resume only sessions owned by this user — by email when
                    // available, falling back to TouristId for legacy rows.
                    if (session == null ||
                        !(session.TouristId == tourist.Id &&
                          (string.Equals(session.UserEmail, userEmail, StringComparison.OrdinalIgnoreCase)
                           || (string.IsNullOrEmpty(session.UserEmail) && string.IsNullOrEmpty(userEmail)))))
                    {
                        session = null;
                    }
                }

                if (session == null)
                {
                    session = new ChatSession
                    {
                        TouristId = tourist.Id,
                        UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail.Trim(),
                        Title = DeriveTitle(request.Message),
                        MessagesJson = "[]",
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    };
                    _chatSessionRepo.Add(session);
                    _chatSessionRepo.Save();
                }
                else if (string.IsNullOrEmpty(session.UserEmail) && !string.IsNullOrWhiteSpace(userEmail))
                {
                    // Self-heal legacy rows: stamp the owner email once we know it.
                    session.UserEmail = userEmail.Trim();
                }

                var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson, _jsonOptions) ?? new();
                if (!string.IsNullOrWhiteSpace(request.Message))
                {
                    messages.Add(new AiChatMessageVM { Role = "user", Content = request.Message });
                }
                if (!string.IsNullOrWhiteSpace(response.Reply))
                {
                    messages.Add(new AiChatMessageVM { Role = "assistant", Content = response.Reply });
                }

                session.MessagesJson = JsonSerializer.Serialize(messages, _jsonOptions);
                session.UpdatedDate = DateTime.Now;
                _chatSessionRepo.Update(session);
                _chatSessionRepo.Save();

                response.ChatSessionId = session.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist chat session for tourist {TouristId}", tourist.Id);
            }
        }

        private static string DeriveTitle(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "New conversation";

            var trimmed = message.Trim();
            if (trimmed.Length <= 40) return trimmed;
            return trimmed.Substring(0, 40).TrimEnd() + "…";
        }
    }
}
