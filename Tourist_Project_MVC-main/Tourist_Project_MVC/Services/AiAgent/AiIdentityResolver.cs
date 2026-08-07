using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Services.AiAgent
{
    /// <summary>
    /// Resolves the AiIdentityContext for the current request. Mirrors the auth
    /// rules that AiChatController used to implement inline:
    ///   - Bearer JWT (React Native mobile app) → validated against the JwtBearer scheme
    ///   - Identity cookie (website)        → CSRF (antiforgery) validated first
    ///   - Anonymous                        → Guest
    /// Role and ownership records are derived from the authenticated identity
    /// ONLY — never from the AI, never from client input.
    /// </summary>
    public interface IAiIdentityResolver
    {
        Task<AiIdentityContext> ResolveAsync(CancellationToken ct = default);
    }

    public class AiIdentityResolver : IAiIdentityResolver
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IAntiforgery _antiforgery;

        public AiIdentityResolver(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            ISponsorRepository sponsorRepo,
            IAntiforgery antiforgery)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _sponsorRepo = sponsorRepo;
            _antiforgery = antiforgery;
        }

        private HttpContext HttpContext =>
            _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context.");

        private bool HasBearerToken()
        {
            return HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader)
                && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<AiIdentityContext> ResolveAsync(CancellationToken ct = default)
        {
            var hasBearerToken = HasBearerToken();
            var hasIdentityCookie = HttpContext.Request.Cookies.ContainsKey(".AspNetCore.Identity.Application");

            ClaimsPrincipal? identity;
            if (hasBearerToken)
            {
                var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (!authResult.Succeeded || authResult.Principal == null)
                    return Guest();
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
                    // CSRF check failed — treat as anonymous; the caller decides.
                    return Guest();
                }
                identity = HttpContext.User;
            }
            else
            {
                identity = HttpContext.User;
            }

            if (identity.Identity?.IsAuthenticated != true)
                return Guest();

            var user = await _userManager.GetUserAsync(identity);
            if (user == null)
                return Guest();

            if (identity.IsInRole(AiIdentityContext.RoleAdmin))
                return new AiIdentityContext { Role = AiIdentityContext.RoleAdmin, User = user };

            if (identity.IsInRole(AiIdentityContext.RoleSponsor))
            {
                var sponsor = _sponsorRepo.GetOrCreateByApplicationUser(user.Id, user.Email);
                return new AiIdentityContext { Role = AiIdentityContext.RoleSponsor, User = user, Sponsor = sponsor };
            }

            if (identity.IsInRole(AiIdentityContext.RoleTourist))
            {
                var tourist = _touristRepo.GetOrCreateByApplicationUser(user);
                return new AiIdentityContext { Role = AiIdentityContext.RoleTourist, User = user, Tourist = tourist };
            }

            return Guest();
        }

        private static AiIdentityContext Guest() => new() { Role = AiIdentityContext.RoleGuest };
    }
}
