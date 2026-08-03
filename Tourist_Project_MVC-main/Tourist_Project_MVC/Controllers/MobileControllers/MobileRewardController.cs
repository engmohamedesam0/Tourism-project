using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileRewardController : ControllerBase
    {
        private readonly TouristContext _context;
        private readonly ITouristRepository _touristRepo;
        private readonly IRewardRepository reward;
        private readonly UserManager<ApplicationUser> _userManager;

        public MobileRewardController(
            TouristContext context,
            IRewardRepository reward,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo)
        {
            _context = context;
            this.reward = reward;
            _userManager = userManager;
            _touristRepo = touristRepo;
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

        [HttpPost("Redeem")]
        public async Task <IActionResult> RedeemReward([FromBody] RedeemRewardDto dto)
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            
            if(applicationUser == null)
            {
                return Unauthorized();
            }

            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var rewardItem = await _context.Rewards.FindAsync(dto.RewardId);

            if(rewardItem == null)
            {
                return NotFound(new { message = "Reward not found." });
            }
            var alreadyRedeemed = await _context.Redemptions
                .AnyAsync(r => r.TouristId == tourist.Id && r.RewardId == dto.RewardId);
            if (alreadyRedeemed)
            {
                return Conflict(new { message = "You have already redeemed this reward." });
            }
            if (rewardItem.ExpirationDate != DateTime.MinValue && rewardItem.ExpirationDate < DateTime.UtcNow)
            {
                return BadRequest(new { message = "This reward has expired." });
            }

            if (rewardItem.QuantityAvailable <= 0)
            {
                return BadRequest(new { message = "This reward is out of stock." });
            }

            if (tourist.point_Balance < rewardItem.PointsRequired)
            {
                return BadRequest(new { message = "Not enough points to redeem this reward." });
            }

            tourist.point_Balance -= rewardItem.PointsRequired;
            rewardItem.QuantityAvailable -= 1;

            var redemption = new Redemption
            {
                RewardId = rewardItem.Id,
                PointsRedeemed = rewardItem.PointsRequired,
                TouristId = tourist.Id,
                Code = GenerateRedemptionCode(),
                Status = "Active",
                RedemptionDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            _context.Redemptions.Add(redemption);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Reward redeemed successfully.",
                code = redemption.Code,
                remainingPoints = tourist.point_Balance
            });
        }

        [HttpGet("MyRedeemed")]
        public async Task<IActionResult> GetMyRedeemedRewards()
        {
            var applicationUser = await _userManager.GetUserAsync(User);

            if (applicationUser == null)
            {
                return Unauthorized();
            }

            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var redemptions = await _context.Redemptions
                .Where(r => r.TouristId == tourist.Id)
                .Include(r => r.Reward)
                .Select(r => new {
                    r.RewardId, 
                    r.Code,
                    r.Status,
                    r.RedemptionDate,
                    r.PointsRedeemed,
                })
                .ToListAsync();

            return Ok(redemptions);
        }
        private static string GenerateRedemptionCode()
        {
            // Simple, readable code — e.g. "A3F9K2". Swap for a different
            // scheme if you need something more secure/collision-resistant.
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
            var random = new Random();
            return new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }
    }
}
