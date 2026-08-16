using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    public class ExploreController : Controller
    {
        private readonly IDestinationRepository _repo;
        private readonly IFavoriteRepository _favoriteRepo;
        private readonly TouristContext _context;

        public ExploreController(IDestinationRepository repo, IFavoriteRepository favoriteRepo, TouristContext context)
        {
            _repo = repo;
            _favoriteRepo = favoriteRepo;
            _context = context;
        }

        public IActionResult Index(string? search)
        {
            var all = _repo.GetAll();

            if (!string.IsNullOrWhiteSpace(search))
            {
                all = all.Where(d =>
                    d.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (d.City != null && d.City.Contains(search, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.Description != null && d.Description.Contains(search, System.StringComparison.OrdinalIgnoreCase)));
            }

            var favoritedIds = new List<int>();
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
                    if (tourist != null)
                    {
                        favoritedIds = _favoriteRepo.GetFavoritedItemIds(tourist.Id, FavoriteItemType.Destination).ToList();
                    }
                }
            }

            ViewBag.Search = search;
            ViewBag.FavoritedIds = favoritedIds;
            return View(all);
        }
    }
}
