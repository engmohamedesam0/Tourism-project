using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleController : Controller
    {
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly TouristContext _context;

        public RoleController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, TouristContext context)
        {
            this.roleManager = roleManager;
            this.userManager = userManager;
            _context = context;
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

        // Unified account-management page: every login account with its current
        // role, plus inline role-change and delete actions.
        [HttpGet]
        public async Task<IActionResult> ManageAccounts()
        {
            var currentAdminId = (await userManager.GetUserAsync(User))?.Id;
            var rows = new List<AccountRow>();

            foreach (var user in userManager.Users.ToList())
            {
                var userRoles = await userManager.GetRolesAsync(user);
                rows.Add(new AccountRow
                {
                    UserId = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    CurrentRole = userRoles.FirstOrDefault() ?? "—",
                    IsCurrentAdmin = user.Id == currentAdminId
                });
            }

            ViewBag.Roles = roleManager.Roles.ToList();
            return View(rows);
        }

        // Reuses the exact role-swap logic from AssignRole: remove all current
        // roles, then add the newly selected one.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageAccounts(UserRoleViewModel model)
        {
            if (!ModelState.IsValid) return await RenderManageAccounts();

            var user = await userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return await RenderManageAccounts();
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            var result = await userManager.AddToRoleAsync(user, model.RoleName);

            if (result.Succeeded) return RedirectToAction("ManageAccounts");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return await RenderManageAccounts();
        }

        // Non-destructive account deletion: the login is removed but the linked
        // Tourist/Sponsor profile and all historical data are preserved by
        // nulling the ApplicationUserId foreign keys first.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var currentAdminId = (await userManager.GetUserAsync(User))?.Id;
            if (id == currentAdminId)
            {
                ModelState.AddModelError("", "You cannot delete your own account.");
                return await RenderManageAccounts();
            }

            var user = await userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            foreach (var tourist in _context.Tourists.Where(t => t.ApplicationUserId == id))
                tourist.ApplicationUserId = null;

            foreach (var sponsor in _context.Sponsors.Where(s => s.ApplicationUserId == id))
                sponsor.ApplicationUserId = null;

            await _context.SaveChangesAsync();
            await userManager.DeleteAsync(user);

            return RedirectToAction("ManageAccounts");
        }

        // Rebuilds the account rows and shared role list for re-rendering the
        // ManageAccounts view after a failed POST.
        private async Task<IActionResult> RenderManageAccounts()
        {
            var currentAdminId = (await userManager.GetUserAsync(User))?.Id;
            var rows = new List<AccountRow>();

            foreach (var user in userManager.Users.ToList())
            {
                var userRoles = await userManager.GetRolesAsync(user);
                rows.Add(new AccountRow
                {
                    UserId = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    CurrentRole = userRoles.FirstOrDefault() ?? "—",
                    IsCurrentAdmin = user.Id == currentAdminId
                });
            }

            ViewBag.Roles = roleManager.Roles.ToList();
            return View("ManageAccounts", rows);
        }
    }
}
