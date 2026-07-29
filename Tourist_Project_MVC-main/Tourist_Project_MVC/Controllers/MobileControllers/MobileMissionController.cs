using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

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

        public MobileMissionController(
            TouristContext context,
            IMissionRepository missionRepository,
            ITouristRepository touristRepository,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _missionRepo = missionRepository;
            _touristRepo = touristRepository;
            _userManager = userManager;
        }
        [HttpGet("AllMissions")]
        public IActionResult AllMissions()
        {
            var missions = _missionRepo.GetAll();

            if (missions == null)
            {
                return BadRequest(new { message = "Unable to retrieve missions." });
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
    }
}
