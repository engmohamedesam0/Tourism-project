using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
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
        private readonly IGamificationService _gamificationService;
        private readonly IConfiguration _config;
        private readonly INotificationService _notificationService;
        private readonly IArcGISSyncService _arcgisSync;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ITouristRepository touristRepo, TouristContext context, IWebHostEnvironment env, IGamificationService gamificationService, IConfiguration config, INotificationService notificationService, IArcGISSyncService arcgisSync)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this._touristRepo = touristRepo;
            this._context = context;
            this._env = env;
            this._gamificationService = gamificationService;
            this._config = config;
            this._notificationService = notificationService;
            this._arcgisSync = arcgisSync;
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

                var applicationUser = new ApplicationUser()
                {
                    UserName = userFromRequest.UserEmail,
                    Email = userFromRequest.UserEmail,
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

                          // Notify every admin that a new sponsor is waiting for approval.
                          _notificationService.CreateForUser(
                              "Admin", null, "NewSponsorApproval",
                              $"New sponsor registration pending approval: {createdUser.FirstName} {createdUser.LastName}.",
                              "SponsorApproval", request.Id);

                          return RedirectToAction("SponsorApprovalStatus", new { status = "submitted" });
                      }
                      else
                      {
                          await userManager.AddToRoleAsync(createdUser, "User");

                          // Link to (or auto-create) the Tourist record for this account so the
                          // Trip planner works immediately after registration. Shared profile
                          // fields (name, nationality, email, photo) live exclusively on the
                          // ApplicationUser identity record — the Tourist record only stores
                          // tourist-specific data and the FK link.
                          var tourist = _touristRepo.GetOrCreateByApplicationUser(createdUser);
                          _touristRepo.Save();

                          // Keep the ArcGIS tourists layers current (per-person table +
                          // aggregated nationality bubbles). Fire-and-forget style: an
                          // ArcGIS hiccup must never break sign-up.
                          try
                          {
                              await _arcgisSync.SyncTouristsTableAsync();
                              await _arcgisSync.SyncTouristNationalityLayerAsync();
                          }
                          catch (Exception)
                          {
                              // Registration must not fail because the sync did; the
                              // admin dashboard "Sync to ArcGIS" button can retry.
                          }
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

                        // Daily login streak tracking + XP award.
                        var loginTourist = _touristRepo.GetOrCreateByApplicationUser(user);
                        var today = DateTime.Today;
                        var progress = await _gamificationService.GetOrInitializeProgressAsync(loginTourist.Id);
                        
                        // Set TempData for Welcome SVG overlay
                        bool isFirstLogin = progress.LastLoginDate == null;
                        TempData["ShowWelcome"] = true;
                        TempData["IsFirstLogin"] = isFirstLogin;

                        if (progress.LastLoginDate == null || progress.LastLoginDate.Value < today.AddDays(-1))
                        {
                            progress.LoginStreak = progress.LastLoginDate.HasValue && progress.LastLoginDate.Value == today.AddDays(-1)
                                ? progress.LoginStreak + 1
                                : 1;
                            progress.LastLoginDate = today;
                            _context.UserProgress.Update(progress);
                            await _context.SaveChangesAsync();
                            await _gamificationService.AwardXPAsync(loginTourist.Id, 10, "daily-login");
                        }

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

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View("ChangePassword");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            if (!ModelState.IsValid)
            {
                return View("ChangePassword", model);
            }

            var user = await userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await signInManager.RefreshSignInAsync(user);
                TempData["PasswordMessage"] = "Your password has been changed successfully.";
                TempData["PasswordMessageType"] = "success";
                return RedirectToAction("Index", "TouristProfile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View("ChangePassword", model);
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

        // =========================================================
        // External (social) login — Google & Facebook
        // =========================================================

        // Step 1: POST from the social buttons — redirects the browser to the
        // external provider (Google / Facebook). The provider sends the user back
        // to ExternalLoginCallback when the flow completes.
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            // The social buttons are always visible, so guard against clicking one
            // whose credentials are not configured yet — without this the browser
            // would bounce to the provider with an invalid client id.
            if (!IsProviderConfigured(provider))
            {
                TempData["SocialLoginError"] =
                    $"{provider} login is not configured yet. Set the ClientId/ClientSecret " +
                    "via .NET User Secrets (dotnet user-secrets set \"Authentication:" + provider +
                    ":ClientId\" ...) and restart the app.";
                return RedirectToAction("Login");
            }

            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        // Step 2: provider redirect target — links the external identity to an
        // ApplicationUser (creating one on first social sign-in), signs the user
        // in, and redirects to the protected page or the Explore home.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ModelState.AddModelError("", $"Error from external provider: {remoteError}");
                return View("Login");
            }

            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ModelState.AddModelError("", "Unable to load external login information. Please try again.");
                return View("Login");
            }

            // Account already linked to this provider → sign straight in.
            var signInResult = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }

            if (signInResult.IsLockedOut)
            {
                ModelState.AddModelError("", "This account is locked out. Please try again later.");
                return View("Login");
            }

            // First sign-in with this provider → create or link the account.
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "The external provider did not return an email address, so an account could not be created.");
                return View("Login");
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "",
                    LastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? ""
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var err in createResult.Errors)
                        ModelState.AddModelError("", err.Description);
                    return View("Login");
                }

                await userManager.AddToRoleAsync(user, "User");
            }

            var addLoginResult = await userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                foreach (var err in addLoginResult.Errors)
                    ModelState.AddModelError("", err.Description);
                return View("Login");
            }

            await signInManager.SignInAsync(user, isPersistent: false);

            // Link/auto-create the Tourist record so the trip planner works
            // immediately after a social sign-in (same as the Register flow).
            _touristRepo.GetOrCreateByApplicationUser(user);
            _touristRepo.Save();

            return RedirectToLocal(returnUrl);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Explore");
        }

        private bool IsProviderConfigured(string provider) =>
            !string.IsNullOrWhiteSpace(_config[$"Authentication:{provider}:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_config[$"Authentication:{provider}:ClientSecret"]);

        [AllowAnonymous]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            return View();
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
