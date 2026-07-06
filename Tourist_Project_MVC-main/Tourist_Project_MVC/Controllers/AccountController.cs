using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
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
                        return RedirectToAction("Index", "Tourist");
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
