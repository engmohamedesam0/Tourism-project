using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.Services.AiAgent;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Backs BOTH the floating AI chat widget on the website AND the React
    // Native mobile app — same URL, same logic, two accepted auth methods:
    //
    //   - Website: ASP.NET Identity cookie + CSRF (antiforgery) header, exactly
    //     as before.
    //   - Mobile app: "Authorization: Bearer {jwt}" header (token obtained from
    //     POST /api/auth/login). No antiforgery check for these requests.
    //
    // The role-aware agent layer (AiIdentityResolver -> AiAgentOrchestrator ->
    // AiToolRegistry -> existing repositories/controllers) derives the current
    // user, role and ownership server-side. The frontend and the AI model never
    // supply identity values.
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly IAiAgentOrchestrator _orchestrator;
        private readonly IAiIdentityResolver _identityResolver;
        private readonly IAiStarterQuestionsService _starterQuestions;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly IChatSessionRepository _chatSessionRepo;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<AiChatController> _logger;

        public AiChatController(
            IAiChatService aiChatService,
            IAiAgentOrchestrator orchestrator,
            IAiIdentityResolver identityResolver,
            IAiStarterQuestionsService starterQuestions,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IChatSessionRepository chatSessionRepo,
            IAntiforgery antiforgery,
            ILogger<AiChatController> logger)
        {
            _aiChatService = aiChatService;
            _orchestrator = orchestrator;
            _identityResolver = identityResolver;
            _starterQuestions = starterQuestions;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _chatSessionRepo = chatSessionRepo;
            _antiforgery = antiforgery;
            _logger = logger;
        }

        private bool HasBearerToken()
        {
            return Request.Headers.TryGetValue("Authorization", out var authHeader)
                && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        }

        // Does this session belong to the given user? Primary key is the
        // email stamped at save time; legacy rows without an email fall back
        // to the Tourist linkage so nothing already stored ever disappears.
        private static bool OwnsSession(ChatSession session, ApplicationUser user, Tourist? tourist)
        {
            if (!string.IsNullOrEmpty(session.UserEmail))
            {
                return string.Equals(session.UserEmail, user.Email, StringComparison.OrdinalIgnoreCase);
            }
            return tourist != null && session.TouristId == tourist.Id;
        }

        // First user message, trimmed, as a one-line preview for the history UI.
        private static string DerivePreview(string messagesJson)
        {
            try
            {
                var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(messagesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                var first = messages.FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                             ?? messages.FirstOrDefault();
                var text = first?.Content?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) return string.Empty;
                return text.Length <= 80 ? text : text.Substring(0, 80).TrimEnd() + "…";
            }
            catch
            {
                return string.Empty;
            }
        }

        // ============================================================
        // Chat
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> Send([FromForm] AiChatRequestVM request, CancellationToken ct)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.Message)
                && (string.IsNullOrWhiteSpace(request.ImagesBase64) || request.ImagesBase64 == "[]")
                && string.IsNullOrWhiteSpace(request.AudioBase64)))
            {
                return BadRequest(new { error = "A message, image, or audio file is required." });
            }

            var identity = await _identityResolver.ResolveAsync(ct);
            if (HasBearerToken() && !identity.IsAuthenticated)
                return Unauthorized(new { error = "Invalid or expired session." });

            // Never trust a ChatSessionId blindly: a stale/foreign id must not
            // abort the user's message, and must not let them continue someone
            // else's conversation.
            if (request.ChatSessionId.HasValue && identity.User != null)
            {
                var session = await _chatSessionRepo.GetByIdAsync(request.ChatSessionId.Value);
                if (session != null)
                {
                    if (!OwnsSession(session, identity.User, identity.Tourist))
                        return StatusCode(403, new { error = "Forbidden" });
                }
                // session == null (deleted/stale) -> the service starts a new conversation.
            }
            else if (request.ChatSessionId.HasValue)
            {
                // Anonymous visitors never persist sessions; ignore the id.
                request.ChatSessionId = null;
            }

            var result = await _aiChatService.GetReplyAsync(request, identity, ct);
            return Json(result);
        }

        // ============================================================
        // Role-aware starter questions (role derived server-side)
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> StarterQuestions(CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            return Json(_starterQuestions.GetForRole(identity.Role));
        }

        // ============================================================
        // Action confirmation (Confirm / Cancel pending actions)
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> ConfirmPendingAction([FromBody] AiConfirmRequestVM model, CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            if (HasBearerToken() && !identity.IsAuthenticated)
                return Unauthorized(new { error = "Invalid or expired session." });

            if (model == null || string.IsNullOrWhiteSpace(model.Token))
                return BadRequest(new { error = "A confirmation token is required." });

            var result = await _orchestrator.ConfirmActionAsync(model.Token.Trim(), identity, ct);
            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> CancelPendingAction([FromBody] AiConfirmRequestVM model, CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            if (HasBearerToken() && !identity.IsAuthenticated)
                return Unauthorized(new { error = "Invalid or expired session." });

            if (model == null || string.IsNullOrWhiteSpace(model.Token))
                return BadRequest(new { error = "A confirmation token is required." });

            var result = await _orchestrator.CancelActionAsync(model.Token.Trim(), identity, ct);
            return Json(result);
        }

        // ============================================================
        // History (unchanged behavior)
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetHistory(CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            var appUser = identity.User;
            if (appUser == null)
            {
                if (HasBearerToken())
                    return Unauthorized(new { error = "Invalid or expired session." });
                return Json(Array.Empty<object>());
            }

            var email = appUser.Email ?? string.Empty;

            // Primary: conversations owned by this email (newest first).
            var byEmail = _chatSessionRepo.GetByUserEmail(email);

            // Legacy: rows saved before the UserEmail column existed but still
            // owned by this user via their Tourist record.
            var tourist = identity.Tourist;
            var legacy = tourist != null
                ? _chatSessionRepo.GetByTouristId(tourist.Id).Where(s => string.IsNullOrEmpty(s.UserEmail))
                : Enumerable.Empty<ChatSession>();

            var sessions = byEmail
                .Concat(legacy)
                .GroupBy(s => s.Id)
                .Select(g => g.First())
                .OrderByDescending(s => s.UpdatedDate)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    Preview = DerivePreview(s.MessagesJson),
                    s.UpdatedDate
                })
                .ToList();

            return Json(sessions);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistorySession(int id, CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            var appUser = identity.User;
            if (appUser == null)
            {
                if (HasBearerToken())
                    return Unauthorized(new { error = "Invalid or expired session." });
                return Json(new { error = "Unauthorized" });
            }

            var session = await _chatSessionRepo.GetByIdAsync(id);
            if (session == null)
                return NotFound();

            if (!OwnsSession(session, appUser, identity.Tourist))
                return StatusCode(403, new { error = "Forbidden" });

            var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return Json(new { id = session.Id, title = session.Title, messages });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSession(int id, CancellationToken ct)
        {
            var identity = await _identityResolver.ResolveAsync(ct);
            var appUser = identity.User;
            if (appUser == null)
            {
                if (HasBearerToken())
                    return Unauthorized(new { error = "Invalid or expired session." });
                return Unauthorized();
            }

            var session = await _chatSessionRepo.GetByIdAsync(id);
            if (session == null)
                return NotFound();

            if (!OwnsSession(session, appUser, identity.Tourist))
                return StatusCode(403, new { error = "Forbidden" });

            _chatSessionRepo.Delete(id);
            _chatSessionRepo.Save();
            return Json(new { ok = true });
        }
    }
}
