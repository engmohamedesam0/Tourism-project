using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileRewardController : ControllerBase
    {
        private readonly TouristContext _context;
        private readonly IRewardRepository reward;
        public MobileRewardController(TouristContext context, IRewardRepository reward)
        {
            _context = context;
            this.reward = reward;
        }
        [HttpGet("AllRewards")]
        public IActionResult AllRewards()
        {
            var rewards = reward.GetAll();
            if (rewards == null)
            {
                return BadRequest(new { message = "Unable to retrieve rewards." });
            }
            var rewardDto = rewards.Select(r => new RewardDTO
            {
                Id = r.Id,
                Type = r.RewardType,
                Title = r.Title,
                Desc = r.Description,
                Points = r.PointsRequired,
                Quntity = r.QuantityAvailable,
                Expiration = r.ExpirationDate,
                Status = r.Status
            }).ToList();
            return Ok(rewardDto);

        }
    }
}
