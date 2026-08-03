using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class TouristController : Controller
    {
        private readonly ITouristRepository _repo;
        private readonly TouristContext _context;
        private readonly IGamificationService _gamificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public TouristController(ITouristRepository repo, TouristContext context, IGamificationService gamificationService, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _repo = repo;
            _context = context;
            _gamificationService = gamificationService;
            _userManager = userManager;
            _env = env;
        }

        public IActionResult Index(string? search, string? nationality)
        {

            var all = _repo.GetAllWithDetails();

            ViewBag.AllCount = all.Count();
            ViewBag.Nationalities = all
                .Select(t => t.Nationality)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ViewBag.Search = search;
            ViewBag.Nationality = nationality;

            if (!string.IsNullOrEmpty(search))
                all = all.Where(t =>
                    t.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Nationality.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(nationality))
                all = all.Where(t => t.Nationality == nationality);

            // Top stat-box row (real aggregates, query-level counts).
            // "Active Today" isn't tracked (no last-activity field on Tourist),
            // so we surface "Active Accounts" (Status = Active) instead; and
            // "Retention Rate" isn't computable, so we show "% Active".
            var now = DateTime.Now;
            var total = _context.Tourists.Count();
            var newThisMonth = _context.Tourists.Count(t =>
                t.RegisterDate.Year == now.Year && t.RegisterDate.Month == now.Month);
            var active = _context.Tourists.Count(t => t.Status == "Active");
            var pctActive = total > 0 ? Math.Round(active * 100.0 / total) : 0;

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-people-fill", Color = "blue",  Value = total.ToString("N0"),        Label = "Total Tourists" },
                new StatBoxItem { IconClass = "bi-person-plus-fill", Color = "green", Value = newThisMonth.ToString("N0"), Label = "New This Month" },
                new StatBoxItem { IconClass = "bi-person-check-fill", Color = "gold", Value = active.ToString("N0"),  Label = "Active Accounts" },
                new StatBoxItem { IconClass = "bi-graph-up", Color = "purple", Value = pctActive.ToString("N0") + "%", Label = "% Active" }
            };

            var touristList = all.ToList();
            var touristIds = touristList.Select(t => t.Id).ToList();
            var progressDict = _context.UserProgress
                .Where(up => touristIds.Contains(up.TouristId))
                .ToDictionary(up => up.TouristId, up => up);
            ViewBag.ProgressDict = progressDict;

            return View(touristList);
        }

        public async Task<IActionResult> Details(int id)
        {
            var tourist = _repo.GetByIdWithDetails(id);
            if (tourist == null) return NotFound();

            var progress = await _gamificationService.GetOrInitializeProgressAsync(tourist.Id);
            var badges = await _gamificationService.GetBadgesForTouristAsync(tourist.Id);
            ViewBag.DetailProgress = progress;
            ViewBag.DetailBadges = badges;

            return View(tourist);
        }

        public IActionResult Create() => View(new CreateTouristViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTouristViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                    return View(model);
                }

                // Handle optional profile picture upload
                string? profilePicturePath = null;
                if (model.ProfilePicture != null && model.ProfilePicture.Length > 0)
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var ext = Path.GetExtension(model.ProfilePicture.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        ModelState.AddModelError("ProfilePicture", "Only image files are allowed.");
                        return View(model);
                    }
                    if (model.ProfilePicture.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfilePicture", "Image must be 2 MB or smaller.");
                        return View(model);
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.ProfilePicture.CopyToAsync(stream);
                    }
                    profilePicturePath = $"/uploads/profile-pictures/{fileName}";
                }

                // Create Identity ApplicationUser
                var appUser = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    Nationality = model.Nationality,
                    ProfilePicturePath = profilePicturePath
                };

                var result = await _userManager.CreateAsync(appUser, model.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }

                await _userManager.AddToRoleAsync(appUser, "User");

                // Create Tourist Record linked to ApplicationUser
                var tourist = new Tourist
                {
                    ApplicationUserId = appUser.Id,
                    IdNumber = model.IdNumber,
                    Passport = model.Passport,
                    PreferredLanguage = model.PreferredLanguage,
                    TravelInterests = model.TravelInterests,
                    RegisterDate = DateTime.Now,
                    Status = "Active"
                };

                _repo.Add(tourist);
                _repo.Save();

                // Initialize Gamification Progress
                await _gamificationService.GetOrInitializeProgressAsync(tourist.Id);

                return RedirectToAction("Index");
            }
            return View(model);
        }

        public IActionResult Edit(int id)
        {
            var tourist = _repo.GetByIdWithDetails(id);
            if (tourist == null) return NotFound();

            var vm = new EditTouristViewModel
            {
                Id = tourist.Id,
                ApplicationUserId = tourist.ApplicationUserId,
                FirstName = tourist.ApplicationUser?.FirstName ?? string.Empty,
                LastName = tourist.ApplicationUser?.LastName ?? string.Empty,
                Email = tourist.ApplicationUser?.Email ?? string.Empty,
                PhoneNumber = tourist.ApplicationUser?.PhoneNumber,
                Nationality = tourist.ApplicationUser?.Nationality ?? string.Empty,
                ExistingProfilePicturePath = tourist.ApplicationUser?.ProfilePicturePath,
                IdNumber = tourist.IdNumber,
                Passport = tourist.Passport,
                point_Balance = tourist.point_Balance,
                Status = tourist.Status ?? "Active",
                PreferredLanguage = tourist.PreferredLanguage,
                TravelInterests = tourist.TravelInterests,
                RegisterDate = tourist.RegisterDate
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditTouristViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var tourist = _context.Tourists
                .Include(t => t.ApplicationUser)
                .FirstOrDefault(t => t.Id == model.Id);

            if (tourist == null) return NotFound();

            // 1. Update User Table data (Source of Truth: ApplicationUser / Users Table)
            if (tourist.ApplicationUser != null)
            {
                // Check if email changed and if it is already taken by another account
                if (!string.Equals(tourist.ApplicationUser.Email, model.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email);
                    if (existingUser != null && existingUser.Id != tourist.ApplicationUser.Id)
                    {
                        ModelState.AddModelError("Email", "An account with this email address already exists.");
                        return View(model);
                    }

                    tourist.ApplicationUser.Email = model.Email;
                    tourist.ApplicationUser.UserName = model.Email;
                }

                tourist.ApplicationUser.FirstName = model.FirstName;
                tourist.ApplicationUser.LastName = model.LastName;
                tourist.ApplicationUser.PhoneNumber = model.PhoneNumber;
                tourist.ApplicationUser.Nationality = model.Nationality;

                // Optional Profile Picture Upload
                if (model.ProfilePicture != null && model.ProfilePicture.Length > 0)
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var ext = Path.GetExtension(model.ProfilePicture.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        ModelState.AddModelError("ProfilePicture", "Only image files are allowed.");
                        return View(model);
                    }
                    if (model.ProfilePicture.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfilePicture", "Image must be 2 MB or smaller.");
                        return View(model);
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await model.ProfilePicture.CopyToAsync(stream);
                    }
                    tourist.ApplicationUser.ProfilePicturePath = $"/uploads/profile-pictures/{fileName}";
                }

                var userUpdateResult = await _userManager.UpdateAsync(tourist.ApplicationUser);
                if (!userUpdateResult.Succeeded)
                {
                    foreach (var err in userUpdateResult.Errors)
                    {
                        ModelState.AddModelError("", err.Description);
                    }
                    return View(model);
                }
            }

            // 2. Update Tourist Table data (Source of Truth: Tourist Table)
            tourist.IdNumber = model.IdNumber;
            tourist.Passport = model.Passport;
            tourist.point_Balance = model.point_Balance;
            tourist.Status = model.Status;
            tourist.PreferredLanguage = model.PreferredLanguage;
            tourist.TravelInterests = model.TravelInterests;

            _repo.Update(tourist);
            _repo.Save();

            return RedirectToAction("Index");
        }

        public IActionResult Delete(int id)
        {
            var tourist = _context.Tourists
                .Include(t => t.ApplicationUser)
                .FirstOrDefault(t => t.Id == id);
            if (tourist == null) return NotFound();
            return View(tourist);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tourist = _context.Tourists
                .Include(t => t.ApplicationUser)
                .FirstOrDefault(t => t.Id == id);

            if (tourist == null) return RedirectToAction("Index");

            // Clean up all related entities referencing TouristId to prevent orphaned records & FK constraints
            var userProgress = _context.UserProgress.Where(up => up.TouristId == id);
            _context.UserProgress.RemoveRange(userProgress);

            var userBadges = _context.UserBadges.Where(ub => ub.TouristId == id);
            _context.UserBadges.RemoveRange(userBadges);

            var userMissions = _context.UserMissions.Where(um => um.TouristId == id);
            _context.UserMissions.RemoveRange(userMissions);

            var tripPlans = _context.TripPlans.Include(tp => tp.TripDestinations).Where(tp => tp.TouristId == id).ToList();
            foreach (var tp in tripPlans)
            {
                if (tp.TripDestinations != null && tp.TripDestinations.Any())
                {
                    _context.TripDestinations.RemoveRange(tp.TripDestinations);
                }
            }
            _context.TripPlans.RemoveRange(tripPlans);

            var redemptions = _context.Redemptions.Where(r => r.TouristId == id);
            _context.Redemptions.RemoveRange(redemptions);

            var siteReviews = _context.SiteReviews.Where(sr => sr.TouristId == id);
            _context.SiteReviews.RemoveRange(siteReviews);

            var favorites = _context.Favorites.Where(f => f.TouristId == id);
            _context.Favorites.RemoveRange(favorites);

            var chatSessions = _context.ChatSessions.Where(cs => cs.TouristId == id);
            _context.ChatSessions.RemoveRange(chatSessions);

            var supportTickets = _context.SupportTickets.Where(st => st.TouristId == id);
            _context.SupportTickets.RemoveRange(supportTickets);

            // Remove Tourist entity
            var appUser = tourist.ApplicationUser;
            _context.Tourists.Remove(tourist);
            await _context.SaveChangesAsync();

            // Also delete the linked ApplicationUser account from Users table
            if (appUser != null)
            {
                await _userManager.DeleteAsync(appUser);
            }

            return RedirectToAction("Index");
        }
    }
}