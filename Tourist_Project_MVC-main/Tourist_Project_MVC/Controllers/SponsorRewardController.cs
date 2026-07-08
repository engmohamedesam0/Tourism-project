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
    public class SponsorRewardController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly IBranchRepository _branchRepo;
        private readonly TouristContext _context;

        public SponsorRewardController(ISponsorRepository sponsorRepo, IRewardRepository rewardRepo, IBranchRepository branchRepo, TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _rewardRepo = rewardRepo;
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

            var rewards = _rewardRepo.GetBySponsorId(sponsor.Id);
            return View("Index", rewards);
        }

        public IActionResult Create()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var branches = _branchRepo.GetBySponsorId(sponsor.Id);
            var vm = new SponsorRewardVM
            {
                SponsorId = sponsor.Id,
                AvailableBranches = branches.ToList()
            };
            return View("Create", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SponsorRewardVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            if (ModelState.IsValid)
            {
                var reward = new Reward
                {
                    Title = vm.Title,
                    RewardType = vm.RewardType,
                    Description = vm.Description,
                    PointsRequired = vm.PointsRequired,
                    QuantityAvailable = vm.QuantityAvailable,
                    ExpirationDate = vm.ExpirationDate,
                    Status = vm.Status,
                    SponsorId = sponsor.Id
                };

                _rewardRepo.Add(reward);
                _rewardRepo.Save();

                SyncBranches(reward.Id, vm.SelectedBranchIds, sponsor.Id);

                return RedirectToAction("Index");
            }

            vm.AvailableBranches = _branchRepo.GetBySponsorId(sponsor.Id).ToList();
            return View("Create", vm);
        }

        public IActionResult Edit(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var reward = _rewardRepo.GetByIdWithBranches(id);
            if (reward == null || reward.SponsorId != sponsor.Id)
                return NotFound();

            var vm = new SponsorRewardVM
            {
                Id = reward.Id,
                Title = reward.Title,
                RewardType = reward.RewardType,
                Description = reward.Description,
                PointsRequired = reward.PointsRequired,
                QuantityAvailable = reward.QuantityAvailable,
                ExpirationDate = reward.ExpirationDate,
                Status = reward.Status,
                SponsorId = reward.SponsorId,
                SelectedBranchIds = reward.RewardBranches?.Select(rb => rb.BranchId).ToList() ?? new(),
                AvailableBranches = _branchRepo.GetBySponsorId(sponsor.Id).ToList()
            };
            return View("Edit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SponsorRewardVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var reward = _context.Rewards.FirstOrDefault(r => r.Id == vm.Id);
            if (reward == null || reward.SponsorId != sponsor.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                reward.Title = vm.Title;
                reward.RewardType = vm.RewardType;
                reward.Description = vm.Description;
                reward.PointsRequired = vm.PointsRequired;
                reward.QuantityAvailable = vm.QuantityAvailable;
                reward.ExpirationDate = vm.ExpirationDate;
                reward.Status = vm.Status;

                _rewardRepo.Update(reward);
                _rewardRepo.Save();

                SyncBranches(reward.Id, vm.SelectedBranchIds, sponsor.Id);

                return RedirectToAction("Index");
            }

            vm.AvailableBranches = _branchRepo.GetBySponsorId(sponsor.Id).ToList();
            return View("Edit", vm);
        }

        public IActionResult Delete(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var reward = _rewardRepo.GetByIdWithBranches(id);
            if (reward == null || reward.SponsorId != sponsor.Id)
                return NotFound();

            return View("Delete", reward);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var reward = _context.Rewards.FirstOrDefault(r => r.Id == id);
            if (reward == null || reward.SponsorId != sponsor.Id)
                return NotFound();

            reward.Status = "Removed";
            _rewardRepo.Update(reward);
            _rewardRepo.Save();
            return RedirectToAction("Index");
        }

        private void SyncBranches(int rewardId, List<int> selectedBranchIds, int sponsorId)
        {
            var validBranchIds = _branchRepo.GetBySponsorId(sponsorId)
                .Select(b => b.Id)
                .ToHashSet();

            var safeSelected = selectedBranchIds
                .Where(id => validBranchIds.Contains(id))
                .Distinct()
                .ToList();

            var existing = _context.RewardBranches
                .Where(rb => rb.RewardId == rewardId)
                .ToList();

            _context.RewardBranches.RemoveRange(existing);

            foreach (var branchId in safeSelected)
            {
                _context.RewardBranches.Add(new RewardBranch
                {
                    RewardId = rewardId,
                    BranchId = branchId
                });
            }

            _context.SaveChanges();
        }
    }
}
