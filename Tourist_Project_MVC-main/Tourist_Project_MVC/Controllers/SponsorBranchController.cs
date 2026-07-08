using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                    Lat = vm.Lat,
                    Long = vm.Long,
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
                Lat = branch.Lat,
                Long = branch.Long,
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
                branch.Lat = vm.Lat;
                branch.Long = vm.Long;
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
