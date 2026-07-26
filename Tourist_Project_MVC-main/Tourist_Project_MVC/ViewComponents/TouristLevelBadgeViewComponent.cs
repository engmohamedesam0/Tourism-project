using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.ViewComponents
{
    public class TouristLevelBadgeViewComponent : ViewComponent
    {
        private readonly TouristContext _context;
        private readonly ITouristRepository _touristRepo;
        private readonly IGamificationService _gamificationService;

        public TouristLevelBadgeViewComponent(
            TouristContext context,
            ITouristRepository touristRepo,
            IGamificationService gamificationService)
        {
            _context = context;
            _touristRepo = touristRepo;
            _gamificationService = gamificationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity.IsAuthenticated || User.IsInRole("Admin") || User.IsInRole("Sponsor"))
            {
                return Content(string.Empty);
            }

            var claimsUser = User as ClaimsPrincipal;
            var userId = claimsUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Content(string.Empty);
            }

            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId)
                ?? _touristRepo.GetOrCreateByApplicationUser(
                    _context.Users.FirstOrDefault(u => u.Id == userId));

            if (tourist == null)
            {
                return Content(string.Empty);
            }

            var progress = await _gamificationService.GetOrInitializeProgressAsync(tourist.Id);
            var (level, name, icon) = LevelDefinitions.GetLevel(progress.CurrentXP);

            ViewData["LevelName"] = name;
            ViewData["LevelIcon"] = icon;
            ViewData["CurrentXP"] = progress.CurrentXP;
            ViewData["NextLevelXP"] = LevelDefinitions.GetNextLevelXP(progress.CurrentXP);

            return View();
        }
    }
}
