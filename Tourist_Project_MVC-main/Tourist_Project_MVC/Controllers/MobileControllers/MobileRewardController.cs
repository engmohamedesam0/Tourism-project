using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MobileRewardController : ControllerBase
    {
        private readonly TouristContext _context;
        private readonly IRewardRepository reward;
        public MobileRewardController(TouristContext context, IRewardRepository reward)
        {
            _context = context;
            this.reward = reward;
        }
        [Authorize]
        [HttpGet]
        public IActionResult AllRewards()
        {
            var missions = reward.GetAll();
            if (missions != null)
            {
                return Ok(missions);
            }
            return BadRequest(new { message = "Unable to retrieve rewards." });
        }
    }
}
