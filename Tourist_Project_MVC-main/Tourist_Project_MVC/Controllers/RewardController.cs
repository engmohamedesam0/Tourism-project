using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class RewardController : Controller
    {
        private readonly IRewardRepository _repo;
        private readonly ISponsorRepository SponserRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly TouristContext _context;

        public RewardController(IRewardRepository repo, ISponsorRepository SponserRepo,
            UserManager<ApplicationUser> userManager, ITouristRepository touristRepo,
            TouristContext context)
        {
            _repo = repo;
            this.SponserRepo = SponserRepo;
            _userManager = userManager;
            _touristRepo = touristRepo;
            _context = context;
        }
        public IActionResult Index(string? search, string? rewardType)
        {
            IEnumerable<Reward> Rewards = _repo.GetAll();

            // Filter Bar Data
            ViewBag.AllCount = Rewards.Count();
            ViewBag.RewardTypes = Rewards
                .Select(r => r.RewardType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            ViewBag.Search = search;
            ViewBag.RewardType = rewardType;

            if (!string.IsNullOrEmpty(search))
                Rewards = Rewards.Where(r =>
                    r.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(rewardType))
                Rewards = Rewards.Where(r => r.RewardType == rewardType);

            // Top stat-box row (real aggregates, query-level).
            var total = _context.Rewards.Count();
            var avgPoints = total > 0 ? Math.Round(_context.Rewards.Average(r => r.PointsRequired)) : 0;

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-gift-fill", Color = "blue", Value = total.ToString("N0"), Label = "Total Rewards" },
                new StatBoxItem { IconClass = "bi-check-circle-fill", Color = "green", Value = _context.Rewards.Count(r => r.Status == "Active").ToString("N0"), Label = "Active Rewards" },
                new StatBoxItem { IconClass = "bi-receipt-fill", Color = "gold", Value = _context.Redemptions.Count().ToString("N0"), Label = "Total Redemptions" },
                new StatBoxItem { IconClass = "bi-coin", Color = "purple", Value = avgPoints.ToString("N0"), Label = "Avg Points Required" }
            };

            return View("Index", Rewards);
        }

        public async Task<IActionResult> Details(int id)
        {
            Reward Reward = _repo.GetById(id);
            if (Reward == null) return NotFound();

            // Log a "reward detail view" for the sponsor dashboard metric.
            // Only authenticated tourists are linked; everyone else is anonymous.
            if (User.Identity.IsAuthenticated)
            {
                string? touristId = null;
                if (User.IsInRole("User"))
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        var tourist = _touristRepo.GetOrCreateByApplicationUser(user);
                        touristId = tourist.Id.ToString();
                    }
                }

                _context.RewardViews.Add(new RewardView
                {
                    RewardId = Reward.Id,
                    TouristId = touristId,
                    ViewedDate = DateTime.Now
                });
                _context.SaveChanges();
            }

            return View("Details", Reward);
        }

        public IActionResult Create()
        {
            AddNewRewardVM NewReward = new AddNewRewardVM
            {
                Sponsors = SponserRepo.GetAll()
            };
            return View("Create", NewReward);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AddNewRewardVM NewReward)
        {
            if (ModelState.IsValid)
            {
                Reward Reward = new Reward
                {
                    Title = NewReward.Title,
                    RewardType = NewReward.RewardType,
                    Description = NewReward.Description,
                    PointsRequired = NewReward.PointsRequired,
                    QuantityAvailable = NewReward.QuantityAvailable,
                    SponsorId = NewReward.SponsorId,
                    ExpirationDate = NewReward.ExpirationDate,
                };
                _repo.Add(Reward);
                _repo.Save();
                return RedirectToAction("Index");
            }
            NewReward.Sponsors = SponserRepo.GetAll();
            return View("Create", NewReward);
        }

        public IActionResult Edit(int id)
        {
            Reward RewardFromDB = _repo.GetById(id);
            if (RewardFromDB == null) return NotFound();

            AddNewRewardVM rewardVM = new AddNewRewardVM
            {
                Id = RewardFromDB.Id,
                Title = RewardFromDB.Title,
                RewardType = RewardFromDB.RewardType,
                Description = RewardFromDB.Description,
                PointsRequired = RewardFromDB.PointsRequired,
                QuantityAvailable = RewardFromDB.QuantityAvailable,
                ExpirationDate = RewardFromDB.ExpirationDate,
                SponsorId = RewardFromDB.SponsorId,
                Sponsors = SponserRepo.GetAll()
            };
            return View("Edit", rewardVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(AddNewRewardVM rewardVM)
        {
            if (ModelState.IsValid)
            {
                Reward Reward = new Reward
                {
                    Id = rewardVM.Id,
                    Title = rewardVM.Title,
                    RewardType = rewardVM.RewardType,
                    Description = rewardVM.Description,
                    PointsRequired = rewardVM.PointsRequired,
                    QuantityAvailable = rewardVM.QuantityAvailable,
                    ExpirationDate = rewardVM.ExpirationDate,
                    SponsorId = rewardVM.SponsorId
                };
                _repo.Update(Reward);
                _repo.Save();
                return RedirectToAction("Index");
            }
            rewardVM.Sponsors = SponserRepo.GetAll();
            return View("Edit", rewardVM);
        }

        public IActionResult Delete(int id)
        {
            Reward Reward = _repo.GetById(id);
            if (Reward == null) return NotFound();
            return View("Delete", Reward);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.Delete(id);
            _repo.Save();
            return RedirectToAction("Index");
        }
    }
}