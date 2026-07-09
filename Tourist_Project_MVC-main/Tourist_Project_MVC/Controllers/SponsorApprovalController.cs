using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SponsorApprovalController : Controller
    {
        private readonly TouristContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public SponsorApprovalController(TouristContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // Lists all sponsor approval requests (newest first), with Approve/Reject
        // actions available only while the request is still Pending.
        public async Task<IActionResult> Index()
        {
            var requests = await _context.SponsorApprovalRequests
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var items = requests.Select(r =>
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == r.ApplicationUserId);
                var name = user != null
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : "(unknown)";
                return new SponsorApprovalListItem
                {
                    Id = r.Id,
                    ApplicantName = name,
                    Email = user?.Email ?? string.Empty,
                    PhoneNumber = user?.PhoneNumber ?? string.Empty,
                    Nationality = user?.Nationality ?? string.Empty,
                    RequestedDate = r.RequestedDate,
                    Status = r.Status
                };
            }).ToList();

            return View("Index", items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.SponsorApprovalRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null || request.Status != "Pending")
                return NotFound();

            var user = await _userManager.FindByIdAsync(request.ApplicationUserId);
            if (user == null)
                return NotFound();

            // Assign the Sponsor role now so the next login routes them into the
            // existing "complete your sponsor profile" flow (SponsorPortal).
            if (!await _roleManager.RoleExistsAsync("Sponsor"))
                await _roleManager.CreateAsync(new IdentityRole("Sponsor"));

            if (!await _userManager.IsInRoleAsync(user, "Sponsor"))
                await _userManager.AddToRoleAsync(user, "Sponsor");

            request.Status = "Approved";
            request.ReviewedDate = DateTime.Now;
            request.ReviewedByAdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _context.Update(request);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.SponsorApprovalRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (request == null || request.Status != "Pending")
                return NotFound();

            // Leave the account without the Sponsor role; it stays blocked at login.
            request.Status = "Rejected";
            request.ReviewedDate = DateTime.Now;
            request.ReviewedByAdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _context.Update(request);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
