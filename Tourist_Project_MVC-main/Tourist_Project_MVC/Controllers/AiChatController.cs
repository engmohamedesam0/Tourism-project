using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        private readonly IAntiforgery _antiforgery;

        public AiChatController(
            IAiChatService aiChatService,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IAntiforgery antiforgery)
        {
            _aiChatService = aiChatService;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _antiforgery = antiforgery;
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] AiChatRequestVM request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required." });
            }

            var hasBearerToken = Request.Headers.TryGetValue("Authorization", out var authHeader)
                && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            System.Security.Claims.ClaimsPrincipal identity;

            if (hasBearerToken)
            {
                // Mobile app request: validate the JWT explicitly (this endpoint
                // isn't [Authorize], so the framework won't do it for us).
                var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (!authResult.Succeeded || authResult.Principal == null)
                {
                    return Unauthorized(new { error = "Invalid or expired token. Please log in again." });
                }
                identity = authResult.Principal;
            }
            else
            {
                // Browser request: enforce CSRF protection as usual.
                try
                {
                    await _antiforgery.ValidateRequestAsync(HttpContext);
                }
                catch (AntiforgeryValidationException)
                {
                    return BadRequest(new { error = "Invalid or missing anti-forgery token." });
                }
                identity = User;
            }

            Tourist? tourist = null;

            // Only resolve/auto-create a Tourist record for users actually signed
            // in with the "User" role — mirrors TripController's pattern, but we
            // must not do this for admins/sponsors/anonymous visitors just for chatting.
            if (identity.Identity?.IsAuthenticated == true && identity.IsInRole("User"))
            {
                var appUser = await _userManager.GetUserAsync(identity);
                if (appUser != null)
                {
                    tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
                }
            }

            var result = await _aiChatService.GetReplyAsync(request, tourist, ct);
            return Json(result);
        }
    }
}
