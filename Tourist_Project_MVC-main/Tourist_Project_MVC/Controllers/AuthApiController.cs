using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Token issuance for non-browser clients (the React Native app). The
    // website keeps using its existing cookie-based Identity login — this
    // endpoint is only for clients that can't hold a cookie.
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        // Access tokens are long-lived (no refresh-token flow yet) to keep the
        // mobile app simple while it only needs the AI chat feature. If you
        // add more sensitive mobile features later, swap this for a short-lived
        // access token + refresh token pair.
        private const int TokenExpiryDays = 30;

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _config;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] MobileLoginRequestVM request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null)
            {
                return Unauthorized(new { error = "Invalid email or password." });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                return Unauthorized(new { error = "Invalid email or password." });
            }

            // Respects lockout / not-allowed rules the same way the website's
            // cookie login does, without actually issuing a cookie.
            if (!await _signInManager.CanSignInAsync(user))
            {
                return Unauthorized(new { error = "This account can't sign in right now." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var expiresAtUtc = DateTime.UtcNow.AddDays(TokenExpiryDays);

            string token;
            try
            {
                token = GenerateJwt(user, roles, expiresAtUtc);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

            return Ok(new MobileLoginResponseVM
            {
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            });
        }

        private string GenerateJwt(ApplicationUser user, IList<string> roles, DateTime expiresAtUtc)
        {
            var jwtKey = _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "Jwt:Key is not configured. Set it via 'dotnet user-secrets set Jwt:Key \"...\"'.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
