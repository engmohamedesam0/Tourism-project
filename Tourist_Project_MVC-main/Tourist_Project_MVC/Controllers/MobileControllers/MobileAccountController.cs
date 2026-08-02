using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql.BackendMessages;
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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManagerr;
        private readonly ILogger<MobileAccountController> logger;
        private readonly IConfiguration _config;
        private readonly TouristContext _context;
        private readonly ITouristRepository _touristRepo;
        private readonly IWebHostEnvironment _environment;
        public MobileAccountController
            (
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<MobileAccountController> logger,
            IConfiguration config,
            TouristContext context,
            ITouristRepository touristRepo,
            IWebHostEnvironment environment
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManagerr = roleManager;
            this.logger = logger;
            _config = config;
            _context = context;
            _touristRepo = touristRepo;
            _environment = environment;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid input." });

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
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

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                return BadRequest(new AuthResponseDto { Success = false, Message = errors });
            }
            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var roleErrors = string.Join(" ", roleResult.Errors.Select(e => e.Description));
                logger.LogWarning("Failed to assign 'User' role to {Email}: {Errors}", user.Email, roleErrors);
            }

            var tourist = new Tourist
            {
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

            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });

            return Ok(await BuildAuthResponse(user, "Login successful."));
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
        [HttpPost("ProfilePicture")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfilePicture(
            [FromForm] IFormFile image,
            CancellationToken cancellationToken)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { success = false, message = "An image is required." });

            const long maximumSize = 5 * 1024 * 1024;
            if (image.Length > maximumSize)
                return BadRequest(new { success = false, message = "The image cannot exceed 5 MB." });

            var allowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp"
            };
            var extension = Path.GetExtension(image.FileName);
            if (!allowedTypes.TryGetValue(extension, out var expectedContentType)
                || !string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase)
                || !await HasValidImageSignatureAsync(image, extension, cancellationToken))
            {
                return BadRequest(new { success = false, message = "Only JPG, PNG, and WebP images are allowed." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { success = false, message = "Invalid or expired session." });

            var webRootPath = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var uploadsDirectory = Path.Combine(webRootPath, "uploads", "profile-pictures");
            Directory.CreateDirectory(uploadsDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var physicalPath = Path.Combine(uploadsDirectory, fileName);
            var oldRelativePath = user.ProfilePicturePath;

            try
            {
                await using var stream = new FileStream(physicalPath, FileMode.CreateNew);
                await image.CopyToAsync(stream, cancellationToken);
            }
            catch
            {
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
                throw;
            }

            user.ProfilePicturePath = $"/uploads/profile-pictures/{fileName}";
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                System.IO.File.Delete(physicalPath);
                var errors = string.Join(" ", updateResult.Errors.Select(error => error.Description));
                logger.LogError("Failed to save profile picture for user {UserId}: {Errors}", user.Id, errors);
                return StatusCode(500, new { success = false, message = "Could not save the profile picture." });
            }

            try
            {
                DeletePreviousProfilePicture(oldRelativePath, uploadsDirectory);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete the previous profile picture for user {UserId}", user.Id);
            }

            return Ok(new
            {
                success = true,
                profilePictureUrl = BuildProfilePictureUrl(user.ProfilePicturePath)
            });
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
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

        private string? BuildProfilePictureUrl(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return $"{Request.Scheme}://{Request.Host}{path}";
        }

        private static async Task<bool> HasValidImageSignatureAsync(
            IFormFile image,
            string extension,
            CancellationToken cancellationToken)
        {
            var header = new byte[12];
            await using var stream = image.OpenReadStream();
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }

            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                return bytesRead >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature);
            }

            return bytesRead >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        }

        private static void DeletePreviousProfilePicture(string? relativePath, string uploadsDirectory)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var oldFileName = Path.GetFileName(relativePath);
            if (string.IsNullOrWhiteSpace(oldFileName))
                return;

            var oldPhysicalPath = Path.Combine(uploadsDirectory, oldFileName);
            if (System.IO.File.Exists(oldPhysicalPath))
                System.IO.File.Delete(oldPhysicalPath);
        }

        private UserDto MapToUserDto(ApplicationUser user) => new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber,
            Country = user.Nationality, // note: property is now "Nationality" per your model, see #3 below
            ProfilePictureUrl = BuildProfilePictureUrl(user.ProfilePicturePath)
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
