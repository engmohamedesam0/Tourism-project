using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

    public class MobileMissionController : ControllerBase
    {
        private readonly TouristContext _context;
        private readonly IMissionRepository _missionRepo;
        private readonly ITouristRepository _touristRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _hmacSecretKey;
        public MobileMissionController(
            TouristContext context,
            IMissionRepository missionRepository,
            ITouristRepository touristRepository,
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory,
            IConfiguration config
            )
        {
            _context = context;
            _missionRepo = missionRepository;
            _touristRepo = touristRepository;
            _userManager = userManager;
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = config["Gemini:ApiKey"];
            _hmacSecretKey = config["AuthMission:VerificationSecret"];
        }
        [HttpGet("AllMissions")]
        public IActionResult AllMissions()
        {
            var missions = _missionRepo.GetAll();

            if (missions == null)
            {
                return NotFound(new { message = "Unable to retrieve missions." });
            }

            var missionDto = missions.Select(m => new MissionDTO
                {
                    Id = m.Id,
                    Title= m.Title,
                    Desc= m.Description,
                    Points= m.PointsReward,
                    MissDestId = m.DestinationId,
                    Type= m.MissionType
                }).ToList();
                return Ok(missionDto);
        }

        [HttpPost("Complete")]
        public async Task<IActionResult> CompleteMission([FromBody] CompleteMissionDto dto)
        {
            var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            var applicationUser = await _userManager.GetUserAsync(User);
            
            if(applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var mission = await _context.Missions.FindAsync(dto.MissionId);

            if(mission == null)
            {
                return NotFound(new { message = "Mission not found" });
            }

            if (string.IsNullOrEmpty(dto.VerificationToken) || string.IsNullOrEmpty(dto.VerificationPayload))
                return BadRequest(new { message = "Photo verification is required before completing this mission." });

            var expectedHash = System.Security.Cryptography.HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_hmacSecretKey),
                Encoding.UTF8.GetBytes(dto.VerificationPayload));
            var expectedToken = Convert.ToBase64String(expectedHash);

            var providedTokenBytes = Convert.FromBase64String(dto.VerificationToken);
            var expectedTokenBytes = Convert.FromBase64String(expectedToken);

            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedTokenBytes, expectedTokenBytes))
                return BadRequest(new { message = "Invalid or tampered verification." });

            var payloadParts = dto.VerificationPayload.Split(':');
            if (payloadParts.Length != 3
                || !int.TryParse(payloadParts[0], out var payloadTouristId)
                || !int.TryParse(payloadParts[1], out var payloadMissionId)
                || !long.TryParse(payloadParts[2], out var payloadTicks))
                return BadRequest(new { message = "Invalid verification payload." });

            if (payloadTouristId != tourist.Id || payloadMissionId != dto.MissionId)
                return BadRequest(new { message = "Verification does not match this mission." });

            var verifiedAt = new DateTime(payloadTicks, DateTimeKind.Utc);
            if (DateTime.UtcNow - verifiedAt > TimeSpan.FromMinutes(10))
                return BadRequest(new { message = "Verification expired. Please verify your photos again." });
            // --- end verification token check ---

            var alreadyCompleted = await _context.UserMissions.AnyAsync(um => um.TouristId == tourist.Id && um.MissionId == dto.MissionId);
            if (alreadyCompleted)
            {
                return Conflict(new { message = "Mission already completed." });
            }
            var userMission = new UserMission()
            {
                TouristId = tourist.Id,
                MissionId = dto.MissionId,
                Status = "Completed",
                PointsEarned = mission.PointsReward,
                Completed_At = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.UserMissions.Add(userMission);

            tourist.point_Balance += mission.PointsReward;
            
            await _context.SaveChangesAsync();
            
            return Ok(new
            {
                message="Mission Completed successfully",
                pointsEarned = userMission.PointsEarned
            });
        }
        
        [HttpGet("MyCompleted")]
        public async Task <IActionResult> MyCompletedMissions()
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if(applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var completed = await _context.UserMissions
                .Where(um => um.TouristId == tourist.Id)
                .Select(um => um.MissionId)
                .ToListAsync();
            return Ok(completed);
        }

        [HttpGet("MyBalance")]
        public async Task <IActionResult> MyPointsBalance()
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }

            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var total = tourist.point_Balance;

            return Ok(new { TotalBalance = total });
        }

        [HttpPost("{id}/verify-photos")]
        public async Task<ActionResult<VerifyPhotosResponse>> VerifyPhotos(int id, [FromBody] VerifyPhotosRequest request)
            {
            var mission = await _missionRepo.GetByIdAsync(id);

            if (mission == null) return NotFound(new { message = "Mission is no longer available." });

            if (request.Images == null || request.Images.Count == 0)
                return BadRequest(new { message = "At least one photo is required." });

            var parts = new List<object>
                {
                    new
                    {
                        text = $"Mission requirement: \"{mission.Description}\". " +
                                $"Look at each of the following {request.Images.Count} photos and decide if it satisfies this mission's requirement. " +
                                "Respond with ONLY a JSON array, no other text, one object per photo in order: " +
                                "[{ \"index\": 0, \"satisfies\": true, \"reason\": \"short explanation\" }, ...]"
                    }
                };


            foreach (var base64 in request.Images)
            {
                parts.Add(new
                {
                    inline_data = new { mime_type = "image/jpeg", data = base64 }
                });
            }

            var payload = new
            {
                contents = new[] { new { parts } },
                generationConfig = new { responseMimeType = "application/json" }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return StatusCode(502, new { message = "Verification service error", details = errorBody });
            }

            var raw = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()!
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();
                var results = JsonSerializer.Deserialize<List<PhotoVerificationResult>>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();

                var applicationUser = await _userManager.GetUserAsync(User);
                if (applicationUser == null)
                {
                    return Unauthorized();
                }
                var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

                bool verified = results.Count > 0 && results.TrueForAll(r => r.Satisfies);
                string? verificationToken = null;
                string? verificationPayload = null;

                if (verified)
                {
                    verificationPayload = $"{tourist.Id}:{id}:{DateTime.UtcNow.Ticks}";
                    var hash = System.Security.Cryptography.HMACSHA256.HashData(
                        Encoding.UTF8.GetBytes(_hmacSecretKey),
                        Encoding.UTF8.GetBytes(verificationPayload));
                    verificationToken = Convert.ToBase64String(hash);
                }

                return Ok(new VerifyPhotosResponse
                {
                    Verified = verified,
                    Results = results,
                    VerificationToken = verificationToken,
                    VerificationPayload = verificationPayload
                });
            }
            catch (Exception)
            {
                return StatusCode(502, new { message = "Verification response was invalid. Please try again." });
            }


        }
    }

}

