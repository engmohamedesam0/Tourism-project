using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.View_Model;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly UserManager<ApplicationUser> userManager;

        public RoleController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            this.roleManager = roleManager;
            this.userManager = userManager;
        }
        [HttpGet]//link
        public IActionResult Create()
        {
            return View("Create");
        }
        [HttpPost]//submit
        public async Task<IActionResult> Create(RoleViewModel roleFromReq)
        {
            if (ModelState.IsValid)
            {
                IdentityRole role = new IdentityRole()
                {
                    Name = roleFromReq.RoleName
                };
                //create db using 
                IdentityResult result = await roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                foreach (var errotItem in result.Errors)
                {
                    ModelState.AddModelError("", errotItem.Description);
                }
            }
            return View("Create", roleFromReq);
        }

        [HttpGet]
        public IActionResult AssignRole()
        {
            var viewModel = new UserRoleViewModel
            {
                Users = userManager.Users.ToList(),
                Roles = roleManager.Roles.ToList()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AssignRole(UserRoleViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await userManager.FindByIdAsync(model.UserId);
                if (user != null)
                {
                    // 1. Get current roles
                    var currentRoles = await userManager.GetRolesAsync(user);
                    // 2. Remove user from all current roles
                    await userManager.RemoveFromRolesAsync(user, currentRoles);
                    // 3. Add to the new role
                    var result = await userManager.AddToRoleAsync(user, model.RoleName);
                    
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Index", "Home");
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "User not found.");
                }
            }

            // Re-populate if something failed
            model.Users = userManager.Users.ToList();
            model.Roles = roleManager.Roles.ToList();
            return View(model);
        }
    }
}
