using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User")]
    public class TouristProfileController : Controller
    {
        private readonly ITouristRepository _touristRepo;
        private readonly TouristContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IGamificationService _gamificationService;

        public TouristProfileController(ITouristRepository touristRepo, TouristContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, IGamificationService gamificationService)
        {
            _touristRepo = touristRepo;
            _context = context;
            _userManager = userManager;
            _env = env;
            _gamificationService = gamificationService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var appUserDetails = _context.Users
                .FirstOrDefault(u => u.Id == tourist.ApplicationUserId);

            var progress = await _gamificationService.GetOrInitializeProgressAsync(tourist.Id);
            var levelInfo = LevelDefinitions.GetLevel(progress.CurrentXP);
            var nextLevelXP = LevelDefinitions.GetNextLevelXP(progress.CurrentXP);
            var allBadges = await _gamificationService.GetBadgesForTouristAsync(tourist.Id);
            var featuredBadge = allBadges.FirstOrDefault(ub => ub.IsFeatured)?.Badge;
            var topBadges = allBadges
                .Where(ub => ub.Badge != null)
                .OrderByDescending(ub => ub.EarnedAt)
                .Take(3)
                .Select(ub => ub.Badge!)
                .ToList();

            var vm = new TouristProfileVM
            {
                Id = tourist.Id,
                Name = appUserDetails != null
                    ? $"{appUserDetails.FirstName} {appUserDetails.LastName}".Trim()
                    : "Unknown",
                FirstName = appUserDetails?.FirstName,
                LastName = appUserDetails?.LastName,
                Email = appUserDetails?.Email ?? string.Empty,
                PhoneNumber = appUserDetails?.PhoneNumber,
                Nationality = appUserDetails?.Nationality ?? string.Empty,
                RegisterDate = tourist.RegisterDate,
                Status = tourist.Status,
                PointBalance = tourist.point_Balance,
                ProfilePicturePath = appUserDetails?.ProfilePicturePath,
                LevelLabel = levelInfo.Name,
                LevelIcon = levelInfo.Icon,
                CurrentLevel = progress.CurrentLevel,
                CurrentXP = progress.CurrentXP,
                NextLevelXP = nextLevelXP,
                TotalBadges = allBadges.Count,
                FeaturedBadge = featuredBadge,
                RecentBadges = topBadges,
                LoginStreak = progress.LoginStreak,
                PreferredLanguage = tourist.PreferredLanguage,
                TravelInterests = tourist.TravelInterests,
                NotifyByEmail = tourist.NotifyByEmail,
                NotifyInApp = tourist.NotifyInApp
            };

            var missionsCompleted = _context.UserMissions
                .Include(um => um.Mission)
                .Where(um => um.TouristId == tourist.Id && um.Status == "Completed")
                .ToList();
            vm.MissionsCompletedCount = missionsCompleted.Count;

            var visitedFromMissions = _context.UserMissions
                .Where(um => um.TouristId == tourist.Id && um.Status == "Completed")
                .Select(um => um.Mission!.DestinationId)
                .Distinct()
                .ToList();

            var visitedFromTrips = _context.TripPlans
                .Include(tp => tp.TripDestinations)
                .Where(tp => tp.TouristId == tourist.Id && tp.Status == "Completed")
                .SelectMany(tp => tp.TripDestinations)
                .Select(td => td.DestinationId)
                .Distinct()
                .ToList();

            var allVisitedIds = visitedFromMissions.Union(visitedFromTrips).Distinct().ToList();
            vm.PlacesVisitedCount = allVisitedIds.Count;

            var redemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Where(r => r.TouristId == tourist.Id)
                .OrderByDescending(r => r.RedemptionDate)
                .ToList();
            vm.RewardsRedeemedCount = redemptions.Count;

            var favCounts = new Dictionary<int, int>();
            foreach (var dId in allVisitedIds)
            {
                if (!favCounts.ContainsKey(dId)) favCounts[dId] = 0;
                favCounts[dId]++;
            }
            var favoriteDestId = favCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            vm.FavoriteDestination = favoriteDestId != 0
                ? _context.Destinations.FirstOrDefault(d => d.Id == favoriteDestId)?.Name
                : "—";

            return View(vm);
        }

        [HttpGet]
        public IActionResult Edit()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var appUserDetails = _context.Users
                .FirstOrDefault(u => u.Id == tourist.ApplicationUserId);

            var vm = new EditTouristProfileVM
            {
                FirstName = appUserDetails?.FirstName ?? string.Empty,
                LastName = appUserDetails?.LastName ?? string.Empty,
                Email = appUserDetails?.Email ?? string.Empty,
                PhoneNumber = appUserDetails?.PhoneNumber ?? string.Empty,
                Nationality = appUserDetails?.Nationality ?? string.Empty,
                PreferredLanguage = tourist.PreferredLanguage,
                TravelInterests = tourist.TravelInterests,
                NotifyByEmail = tourist.NotifyByEmail,
                NotifyInApp = tourist.NotifyInApp,
                ProfilePicturePath = appUserDetails?.ProfilePicturePath
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditTouristProfileVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var appUserDetails = await _userManager.GetUserAsync(User);
            if (appUserDetails == null) return RedirectToAction(nameof(Index));

            if (vm.ProfilePicture != null && vm.ProfilePicture.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = System.IO.Path.GetExtension(vm.ProfilePicture.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError("ProfilePicture", "Only image files are allowed.");
                    return View(vm);
                }
                if (vm.ProfilePicture.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError("ProfilePicture", "Image must be 2 MB or smaller.");
                    return View(vm);
                }

                var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
                System.IO.Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = System.IO.Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await vm.ProfilePicture.CopyToAsync(stream);
                }
                appUserDetails.ProfilePicturePath = $"/uploads/profile-pictures/{fileName}";
            }

            appUserDetails.FirstName = vm.FirstName;
            appUserDetails.LastName = vm.LastName;
            appUserDetails.PhoneNumber = vm.PhoneNumber;
            appUserDetails.Nationality = vm.Nationality;

            var emailResult = await _userManager.SetEmailAsync(appUserDetails, vm.Email);
            if (emailResult.Succeeded)
            {
                await _userManager.SetUserNameAsync(appUserDetails, vm.Email);
            }

            await _userManager.UpdateAsync(appUserDetails);

            // Tourist-specific preferences only — shared profile fields (name,
            // nationality, email, phone, photo) are owned exclusively by
            // ApplicationUser and updated above via UserManager.
            tourist.PreferredLanguage = vm.PreferredLanguage;
            tourist.TravelInterests = vm.TravelInterests;
            tourist.NotifyByEmail = vm.NotifyByEmail;
            tourist.NotifyInApp = vm.NotifyInApp;

            _touristRepo.Update(tourist);
            _touristRepo.Save();

            TempData["ProfileMessage"] = "Profile updated successfully.";
            TempData["ProfileMessageType"] = "success";
            return RedirectToAction(nameof(Index));
        }
    }
}
