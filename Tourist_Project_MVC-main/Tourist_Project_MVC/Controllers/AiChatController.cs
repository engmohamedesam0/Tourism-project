using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    // Deliberately NOT [Authorize]: anonymous visitors (browser or app) can
    // still ask about Egyptian history and places. Only *saving* a trip
    // requires a signed-in Tourist, which AiChatService enforces on its own.
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly IChatSessionRepository _chatSessionRepo;
        private readonly IAntiforgery _antiforgery;

        public AiChatController(
            IAiChatService aiChatService,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IChatSessionRepository chatSessionRepo,
            IAntiforgery antiforgery)
        {
            _aiChatService = aiChatService;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _chatSessionRepo = chatSessionRepo;
            _antiforgery = antiforgery;
        }

        private async Task<Tourist?> ResolveTouristAsync(CancellationToken ct = default)
        {
            var hasBearerToken = Request.Headers.TryGetValue("Authorization", out var authHeader)
                && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

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

            var appUser = await _userManager.GetUserAsync(identity);
            if (appUser == null) return null;

            return _touristRepo.GetOrCreateByApplicationUser(appUser);
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

            var tourist = await ResolveTouristAsync(ct);

            var result = await _aiChatService.GetReplyAsync(request, tourist, ct);
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(CancellationToken ct)
        {
            var tourist = await ResolveTouristAsync(ct);
            if (tourist == null)
                return Json(Array.Empty<object>());

            var sessions = _chatSessionRepo.GetByTouristId(tourist.Id)
                .Select(s => new { s.Id, s.Title, s.UpdatedDate })
                .ToList();
            return Json(sessions);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistorySession(int id, CancellationToken ct)
        {
            var tourist = await ResolveTouristAsync(ct);
            if (tourist == null)
                return Json(new { error = "Unauthorized" });

            var session = await _chatSessionRepo.GetByIdAsync(id);
            if (session == null || session.TouristId != tourist.Id)
                return NotFound();

            var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return Json(new { id = session.Id, title = session.Title, messages });
        }
    }
}
