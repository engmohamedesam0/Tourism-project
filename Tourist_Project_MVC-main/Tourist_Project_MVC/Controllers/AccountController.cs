using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        private readonly ISponsorRepository _sponsorRepo;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, ITouristRepository touristRepo, ISponsorRepository sponsorRepo)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this._touristRepo = touristRepo;
            this._sponsorRepo = sponsorRepo;
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
                var applicationUser = new ApplicationUser()
                {
                    UserName = userFromRequest.UserName,
                    Email = userFromRequest.UserEmail,
                    PasswordHash = userFromRequest.Password
                };
                var identityResult = await userManager.CreateAsync(applicationUser, userFromRequest.Password);
                
                  if (identityResult.Succeeded)
                  {
                      var createdUser = await userManager.FindByNameAsync(applicationUser.UserName);

                      if (userFromRequest.AccountType == "Sponsor")
                      {
                          // Make sure the Sponsor role exists, then assign it.
                          await EnsureRoleAsync("Sponsor");
                          await userManager.AddToRoleAsync(createdUser, "Sponsor");

                          // Link a new Sponsor record to this login account so the
                          // signed-in sponsor's own data resolves directly by FK.
                           var sponsor = new Sponsor
                           {
                               Name = userFromRequest.BusinessName ?? createdUser.UserName,
                               Type = userFromRequest.SponsorType ?? string.Empty,
                               Address = userFromRequest.SponsorAddress ?? string.Empty,
                               ContactNumber = userFromRequest.ContactNumber ?? 0,
                               Email = userFromRequest.UserEmail ?? string.Empty,
                               ApplicationUserId = createdUser.Id
                           };
                          _sponsorRepo.Add(sponsor);
                          _sponsorRepo.Save();
                      }
                      else
                      {
                          await userManager.AddToRoleAsync(createdUser, "User");

                          // Link to (or auto-create) the Tourist record for this account so the
                          // Trip planner works immediately after registration.
                          _touristRepo.GetOrCreateByApplicationUser(createdUser);
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

        // Creates a role if it does not already exist (so Sponsor sign-ups do
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
