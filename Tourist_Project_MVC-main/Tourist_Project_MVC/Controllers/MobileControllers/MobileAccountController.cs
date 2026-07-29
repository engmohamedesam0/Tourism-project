using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class MobileAccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ILogger<MobileAccountController> logger;
        private readonly IConfiguration _config;
        private readonly TouristContext _context;
        private readonly ITouristRepository _touristRepo;
        public MobileAccountController
            (
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<MobileAccountController> logger,
            IConfiguration config,
            TouristContext context,
            ITouristRepository touristRepo
            )
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this.logger = logger;
            _config = config;
            _context = context;
            _touristRepo = touristRepo;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid input." });

            var existingUser = await userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return Conflict(new AuthResponseDto { Success = false, Message = "Email already registered." });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PhoneNumber = dto.Phone,
                Nationality = dto.Country,
            };

            var result = await userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                return BadRequest(new AuthResponseDto { Success = false, Message = errors });
            }
            var roleResult = await userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var roleErrors = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                logger.LogWarning("Failed to assign 'User' role to {Email}: {Errors}", user.Email, roleErrors);
            }

            var tourist = new Tourist
            {
                Name = $"{dto.FirstName} {dto.LastName}".Trim(),
                Email = dto.Email,
                Nationality = dto.Country,
                Password = String.Empty,
                RegisterDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                Status = "Active",
                point_Balance = 0,
                ApplicationUserId = user.Id
            };
            _touristRepo.Add(tourist);
            _touristRepo.Save();

            return Ok(await BuildAuthResponse(user, "Registration successful."));
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid input." });

            var user = await userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });

            var result = await signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });

            return Ok(await BuildAuthResponse(user, "Login successful."));
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("firstName", user.FirstName ?? ""),
            new Claim("lastName", user.LastName ?? "")
        };
            claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresInDays = double.Parse(_config["Jwt:ExpiresInDays"] ?? "7");

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expiresInDays),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static UserDto MapToUserDto(ApplicationUser user) => new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber,
            Country = user.Nationality // note: property is now "Nationality" per your model, see #3 below
        };

        private async Task<AuthResponseDto> BuildAuthResponse(ApplicationUser user, string message)
        {
            var token = await GenerateJwtToken(user); // assuming you applied the async role-claims version from before
            return new AuthResponseDto
            {
                Success = true,
                Token = token,
                Message = message,
                User = MapToUserDto(user)
            };
        }
    }
}
