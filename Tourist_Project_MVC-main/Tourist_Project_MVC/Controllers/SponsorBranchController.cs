using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Sponsor")]
    public class SponsorBranchController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IBranchRepository _branchRepo;
        private readonly TouristContext _context;

        public SponsorBranchController(ISponsorRepository sponsorRepo, IBranchRepository branchRepo, TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _branchRepo = branchRepo;
            _context = context;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        public IActionResult Index()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branches = _branchRepo.GetBySponsorId(sponsor.Id);

            // Top stat-box row (scoped to this sponsor, query-level aggregates).
            var sponsorId = sponsor.Id;
            var totalRedemptions = _context.Redemptions.Count(r => r.Reward != null && r.Reward.SponsorId == sponsorId);

            var topBranchId = _context.Redemptions
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId && r.BranchId != null)
                .GroupBy(r => r.BranchId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
            var topBranchName = topBranchId.HasValue
                ? _context.Branches.Where(b => b.Id == topBranchId.Value).Select(b => b.Name).FirstOrDefault()
                : null;

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-diagram-3-fill", Color = "blue", Value = _context.Branches.Count(b => b.SponsorId == sponsorId).ToString("N0"), Label = "Total Branches" },
                new StatBoxItem { IconClass = "bi-gift-fill", Color = "green", Value = _context.Rewards.Count(r => r.SponsorId == sponsorId).ToString("N0"), Label = "Total Rewards" },
                new StatBoxItem { IconClass = "bi-receipt-fill", Color = "gold", Value = totalRedemptions.ToString("N0"), Label = "Total Redemptions" },
                new StatBoxItem { IconClass = "bi-trophy-fill", Color = "purple", Value = topBranchName ?? "—", Label = "Most Active Branch" }
            };

            return View("Index", branches);
        }

        public IActionResult Create()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            return View("Create", new SponsorBranchVM { SponsorId = sponsor.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SponsorBranchVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            if (ModelState.IsValid)
            {
                var branch = new Branch
                {
                    Name = vm.Name,
                    Address = vm.Address,
                    Location = new Point(vm.Long, vm.Lat) { SRID = 4326 },
                    ContactNumber = vm.ContactNumber,
                    SponsorId = sponsor.Id
                };
                _branchRepo.Add(branch);
                _branchRepo.Save();
                return RedirectToAction("Index");
            }

            vm.SponsorId = sponsor.Id;
            return View("Create", vm);
        }

        public IActionResult Edit(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branch = _branchRepo.GetById(id);
            if (branch == null || branch.SponsorId != sponsor.Id)
                return NotFound();

            var vm = new SponsorBranchVM
            {
                Id = branch.Id,
                Name = branch.Name,
                Address = branch.Address,
                Lat = (float)branch.Location.Y,
                Long = (float)branch.Location.X,
                ContactNumber = branch.ContactNumber,
                SponsorId = branch.SponsorId
            };
            return View("Edit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SponsorBranchVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branch = _context.Branches.FirstOrDefault(b => b.Id == vm.Id);
            if (branch == null || branch.SponsorId != sponsor.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                branch.Name = vm.Name;
                branch.Address = vm.Address;
                branch.Location = new Point(vm.Long, vm.Lat) { SRID = 4326 };
                branch.ContactNumber = vm.ContactNumber;
                _branchRepo.Update(branch);
                _branchRepo.Save();
                return RedirectToAction("Index");
            }

            return View("Edit", vm);
        }

        public IActionResult Delete(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branch = _branchRepo.GetById(id);
            if (branch == null || branch.SponsorId != sponsor.Id)
                return NotFound();

            return View("Delete", branch);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branch = _context.Branches.FirstOrDefault(b => b.Id == id);
            if (branch == null || branch.SponsorId != sponsor.Id)
                return NotFound();

            _branchRepo.Delete(id);
            _branchRepo.Save();
            return RedirectToAction("Index");
        }
    }
}
