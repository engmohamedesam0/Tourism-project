using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User")]
    [Route("Favorites")]
    public class FavoriteController : Controller
    {
        private readonly IFavoriteRepository _favoriteRepo;
        private readonly IDestinationRepository _destRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly IBranchRepository _branchRepo;
        private readonly TouristContext _context;
        private readonly ITouristRepository _touristRepo;

        public FavoriteController(
            IFavoriteRepository favoriteRepo,
            IDestinationRepository destRepo,
            IRewardRepository rewardRepo,
            IBranchRepository branchRepo,
            TouristContext context,
            ITouristRepository touristRepo)
        {
            _favoriteRepo = favoriteRepo;
            _destRepo = destRepo;
            _rewardRepo = rewardRepo;
            _branchRepo = branchRepo;
            _context = context;
            _touristRepo = touristRepo;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            if (tourist == null)
            {
                return Forbid();
            }

            var favDestIds = _favoriteRepo.GetFavoritedItemIds(tourist.Id, FavoriteItemType.Destination);
            var favRewardIds = _favoriteRepo.GetFavoritedItemIds(tourist.Id, FavoriteItemType.Reward);
            var favBranchIds = _favoriteRepo.GetFavoritedItemIds(tourist.Id, FavoriteItemType.Branch);

            var destinations = favDestIds.Select(id => _destRepo.GetById(id)).Where(d => d != null).ToList();
            var rewards = favRewardIds.Select(id => _rewardRepo.GetById(id)).Where(r => r != null).ToList();
            var branches = favBranchIds.Select(id => _branchRepo.GetById(id)).Where(b => b != null).ToList();

            var vm = new FavoritesIndexVM
            {
                Destinations = destinations,
                Rewards = rewards,
                Branches = branches
            };

            return View(vm);
        }

        [HttpPost("Toggle")]
        public async Task<IActionResult> Toggle([FromBody] ToggleFavoriteRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            if (tourist == null)
            {
                return Unauthorized();
            }

            bool itemExists = request.ItemType switch
            {
                FavoriteItemType.Destination => _destRepo.GetById(request.ItemId) != null,
                FavoriteItemType.Reward => _rewardRepo.GetById(request.ItemId) != null,
                FavoriteItemType.Branch => _branchRepo.GetById(request.ItemId) != null,
                _ => false
            };

            if (!itemExists)
            {
                return NotFound(new { error = "Item not found" });
            }

            var existing = _favoriteRepo.Find(tourist.Id, request.ItemType, request.ItemId);
            if (existing != null)
            {
                _favoriteRepo.Delete(existing.Id);
                _favoriteRepo.Save();
                return Json(new { isFavorited = false, itemType = request.ItemType.ToString(), itemId = request.ItemId });
            }

            var favorite = new Favorite
            {
                TouristId = tourist.Id,
                ItemType = request.ItemType,
                ItemId = request.ItemId
            };

            _favoriteRepo.Add(favorite);
            _favoriteRepo.Save();
            return Json(new { isFavorited = true, itemType = request.ItemType.ToString(), itemId = request.ItemId });
        }
    }

    public class ToggleFavoriteRequest
    {
        public FavoriteItemType ItemType { get; set; }
        public int ItemId { get; set; }
    }
}