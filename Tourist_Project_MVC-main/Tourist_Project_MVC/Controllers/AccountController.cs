using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly ITouristRepository _touristRepo;
        private readonly TouristContext _context;
        private readonly IWebHostEnvironment _env;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ITouristRepository touristRepo, TouristContext context, IWebHostEnvironment env)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this._touristRepo = touristRepo;
            this._context = context;
            this._env = env;
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View("Register");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task <IActionResult> Register(RegisterViewModel userFromRequest)
        {
            if (ModelState.IsValid)
            {
                // Optional profile picture upload (image only, reasonable size cap).
                string? profilePicturePath = null;
                var profileFile = userFromRequest.ProfilePicture;
                if (profileFile != null && profileFile.Length > 0)
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var ext = Path.GetExtension(profileFile.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        ModelState.AddModelError("ProfilePicture", "Only image files are allowed.");
                        return View("Register", userFromRequest);
                    }
                    if (profileFile.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfilePicture", "Image must be 2 MB or smaller.");
                        return View("Register", userFromRequest);
                    }

                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
                    Directory.CreateDirectory(uploadsFolder);
                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var fullPath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await profileFile.CopyToAsync(stream);
                    }
                    profilePicturePath = $"/uploads/profile-pictures/{fileName}";
                }

                var userName = $"{userFromRequest.FirstName} {userFromRequest.LastName}".Trim();

                var applicationUser = new ApplicationUser()
                {
                    UserName = userName,
                    Email = userFromRequest.UserEmail,
                    PasswordHash = userFromRequest.Password,
                    PhoneNumber = userFromRequest.PhoneNumber,
                    FirstName = userFromRequest.FirstName,
                    LastName = userFromRequest.LastName,
                    Nationality = userFromRequest.Nationality,
                    ProfilePicturePath = profilePicturePath
                };
                var identityResult = await userManager.CreateAsync(applicationUser, userFromRequest.Password);

                  if (identityResult.Succeeded)
                  {
                      var createdUser = await userManager.FindByNameAsync(applicationUser.UserName);

                      if (userFromRequest.AccountType == "Sponsor")
                      {
                          // Sponsor sign-up is gated by Admin approval: create the
                          // account with shared profile fields but DO NOT assign the
                          // Sponsor role and DO NOT create a Sponsor record yet.
                          var request = new SponsorApprovalRequest
                          {
                              ApplicationUserId = createdUser.Id,
                              Status = "Pending",
                              RequestedDate = DateTime.Now
                          };
                          _context.SponsorApprovalRequests.Add(request);
                          await _context.SaveChangesAsync();

                          return RedirectToAction("SponsorApprovalStatus", new { status = "submitted" });
                      }
                      else
                      {
                          await userManager.AddToRoleAsync(createdUser, "User");

                          // Link to (or auto-create) the Tourist record for this account so the
                          // Trip planner works immediately after registration. Populate it from
                          // the new shared profile fields where columns match.
                          var tourist = _touristRepo.GetOrCreateByApplicationUser(createdUser);
                          tourist.Name = $"{createdUser.FirstName} {createdUser.LastName}".Trim();
                          tourist.Nationality = createdUser.Nationality;
                          tourist.Email = createdUser.Email ?? string.Empty;
                          _touristRepo.Update(tourist);
                          _touristRepo.Save();
                      }

                      return RedirectToAction("Login");
                  }
                foreach (var errorItem in identityResult.Errors)
                {
                    ModelState.AddModelError("", errorItem.Description);
                }
            }
            return View("Register", userFromRequest);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View("Login");
        }
        public async Task <IActionResult> Login(LoginViewModel loginUser)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByNameAsync(loginUser.UserName);
                if (user != null)
                {
                    var passed = await userManager.CheckPasswordAsync(user, loginUser.UserPassword);
                    if(passed)
                    {
                        // Sponsor approval gate: a pending/rejected request short-circuits
                        // any portal redirect and shows a clear status message instead.
                        var approval = await _context.SponsorApprovalRequests
                            .FirstOrDefaultAsync(r => r.ApplicationUserId == user.Id);
                        if (approval != null && approval.Status == "Pending")
                            return RedirectToAction("SponsorApprovalStatus", new { status = "pending" });
                        if (approval != null && approval.Status == "Rejected")
                            return RedirectToAction("SponsorApprovalStatus", new { status = "rejected" });

                        await signInManager.SignInAsync(user, loginUser.RememberMe);

                        // Role-based landing: Admins stay in the back office,
                        // Sponsors land on their own portal, everyone else (Tourists)
                        // land on the new Explore discovery page.
                        if (await userManager.IsInRoleAsync(user, "Admin"))
                            return RedirectToAction("Index", "Tourist");

                        if (await userManager.IsInRoleAsync(user, "Sponsor"))
                            return RedirectToAction("Index", "SponsorPortal");

                        return RedirectToAction("Index", "Explore");
                    }
                }
                ModelState.AddModelError("", "Invalid Account");
            }
            return View("Login", loginUser);
        }

        // Clear status page shown after a Sponsor sign-up and on login attempts
        // for accounts whose SponsorApprovalRequest is still pending/rejected.
        [HttpGet]
        public IActionResult SponsorApprovalStatus(string status)
        {
            ViewData["Status"] = status;
            return View("SponsorApprovalStatus");
        }

        public IActionResult Reset()
        {
            return View("Reset");
        }

        [HttpPost]
        public async Task<IActionResult> Reset(ResetPasswordViewModel resetFromReq)
        {
            var ExistingMail = await userManager.FindByEmailAsync(resetFromReq.UserEmail);
            if (ExistingMail != null)
            {
                return Content("Please Check Your Email For Password Reset Steps.");
            }
            return RedirectToAction("Index", "Home");
        }
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // Creates a role if it does not already exist (so Sponsor approval does
        // not depend on an Admin having created the role first).
        private async Task EnsureRoleAsync(string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
