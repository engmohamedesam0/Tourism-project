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
        private readonly ITouristRepository _touristRepo;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ITouristRepository touristRepo)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this._touristRepo = touristRepo;
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
                     await userManager.AddToRoleAsync(applicationUser, "User");

                     // Link to (or auto-create) the Tourist record for this account so the
                     // Trip planner works immediately after registration.
                     var createdUser = await userManager.FindByNameAsync(applicationUser.UserName);
                     _touristRepo.GetOrCreateByApplicationUser(createdUser);

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
                        // Tourists land on the new Explore discovery page.
                        if (await userManager.IsInRoleAsync(user, "Admin"))
                            return RedirectToAction("Index", "Tourist");

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
    }
}
