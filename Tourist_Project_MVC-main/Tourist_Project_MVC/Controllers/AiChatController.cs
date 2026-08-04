using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Backs BOTH the floating AI chat widget on the website AND the React
    // Native mobile app — same URL, same logic, two accepted auth methods:
    //
    //   - Website: ASP.NET Identity cookie + CSRF (antiforgery) header, exactly
    //     as before.
    //   - Mobile app: "Authorization: Bearer {jwt}" header (token obtained from
    //     POST /api/auth/login). No antiforgery check for these requests — CSRF
    //     protection exists to stop a browser being tricked into resending a
    //     cookie it holds automatically; a bearer token is never attached
    //     automatically by anything, so the same attack doesn't apply.
    //
    // Chat history ownership: every conversation is stamped with the email of
    // the authenticated user (resolved server-side from the auth identity via
    // UserManager — NEVER from frontend input). All history reads/writes/deletes
    // are filtered by that email, so users can only ever see their own chats.
    //
    // Anonymous website visitors can still ask general questions. When a bearer
    // token is supplied by mobile, however, it must be valid and belong to a User.
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly IChatSessionRepository _chatSessionRepo;
        private readonly IAntiforgery _antiforgery;
        private readonly ILogger<AiChatController> _logger;

        public AiChatController(
            IAiChatService aiChatService,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IChatSessionRepository chatSessionRepo,
            IAntiforgery antiforgery,
            ILogger<AiChatController> logger)
        {
            _aiChatService = aiChatService;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _chatSessionRepo = chatSessionRepo;
            _antiforgery = antiforgery;
            _logger = logger;
        }

        // Resolves the authenticated user (ApplicationUser) whose identity is
        // attached to this request — cookie (website, CSRF-validated) or bearer
        // JWT (mobile app). Returns null when there is no valid authenticated
        // "User"-role identity; the caller decides how to treat that.
        private async Task<ApplicationUser?> ResolveAuthenticatedUserAsync()
        {
            var hasBearerToken = HasBearerToken();

            var hasIdentityCookie = Request.Cookies.ContainsKey(".AspNetCore.Identity.Application");

            ClaimsPrincipal identity;

            if (hasBearerToken)
            {
                var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (!authResult.Succeeded || authResult.Principal == null)
                {
                    return null;
                }
                identity = authResult.Principal;
            }
            else if (hasIdentityCookie)
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(HttpContext);
                }
                catch (AntiforgeryValidationException)
                {
                    return null;
                }
                identity = User;
            }
            else
            {
                identity = User;
            }

            if (identity.Identity?.IsAuthenticated != true || !identity.IsInRole("User"))
            {
                return null;
            }

            return await _userManager.GetUserAsync(identity);
        }

        private async Task<Tourist?> ResolveTouristAsync(CancellationToken ct = default)
        {
            var appUser = await ResolveAuthenticatedUserAsync();
            if (appUser == null) return null;

            return _touristRepo.GetOrCreateByApplicationUser(appUser);
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

        [HttpPost]
        public async Task<IActionResult> Send([FromForm] AiChatRequestVM request, CancellationToken ct)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.Message)
                && (string.IsNullOrWhiteSpace(request.ImagesBase64) || request.ImagesBase64 == "[]")
                && string.IsNullOrWhiteSpace(request.AudioBase64)))
            {
                return BadRequest(new { error = "A message, image, or audio file is required." });
            }

            var appUser = await ResolveAuthenticatedUserAsync();
            if (HasBearerToken() && appUser == null)
                return Unauthorized(new { error = "Invalid or expired session." });

            Tourist? tourist = appUser != null ? _touristRepo.GetOrCreateByApplicationUser(appUser) : null;

            // Never trust a ChatSessionId blindly: a stale/foreign id must not
            // abort the user's message, and must not let them continue someone
            // else's conversation.
            if (request.ChatSessionId.HasValue && appUser != null)
            {
                var session = await _chatSessionRepo.GetByIdAsync(request.ChatSessionId.Value);
                if (session != null)
                {
                    if (!OwnsSession(session, appUser, tourist))
                        return StatusCode(403, new { error = "Forbidden" });
                }
                // session == null (deleted/stale) -> service starts a new conversation.
            }
            else if (request.ChatSessionId.HasValue)
            {
                // Anonymous visitors never persist sessions; ignore the id.
                request.ChatSessionId = null;
            }

            var result = await _aiChatService.GetReplyAsync(request, tourist, appUser?.Email, ct);
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(CancellationToken ct)
        {
            var appUser = await ResolveAuthenticatedUserAsync();
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
            var tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            var legacy = _chatSessionRepo.GetByTouristId(tourist.Id)
                .Where(s => string.IsNullOrEmpty(s.UserEmail));

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
            var appUser = await ResolveAuthenticatedUserAsync();
            if (appUser == null)
            {
                if (HasBearerToken())
                    return Unauthorized(new { error = "Invalid or expired session." });
                return Json(new { error = "Unauthorized" });
            }

            var session = await _chatSessionRepo.GetByIdAsync(id);
            if (session == null)
                return NotFound();

            var tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            if (!OwnsSession(session, appUser, tourist))
                return StatusCode(403, new { error = "Forbidden" });

            var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return Json(new { id = session.Id, title = session.Title, messages });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSession(int id, CancellationToken ct)
        {
            var appUser = await ResolveAuthenticatedUserAsync();
            if (appUser == null)
            {
                if (HasBearerToken())
                    return Unauthorized(new { error = "Invalid or expired session." });
                return Unauthorized();
            }

            var session = await _chatSessionRepo.GetByIdAsync(id);
            if (session == null)
                return NotFound();

            var tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            if (!OwnsSession(session, appUser, tourist))
                return StatusCode(403, new { error = "Forbidden" });

            _chatSessionRepo.Delete(id);
            _chatSessionRepo.Save();
            return Json(new { ok = true });
        }
    }
}
