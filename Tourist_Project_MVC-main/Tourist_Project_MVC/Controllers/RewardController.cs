using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class RewardController : Controller
    {
        private readonly IRewardRepository _repo;
        private readonly ISponsorRepository SponserRepo;

        public RewardController(IRewardRepository repo, ISponsorRepository SponserRepo)
        {
            _repo = repo;
            this.SponserRepo = SponserRepo;
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

            return View("Index", Rewards);
        }

        public IActionResult Details(int id)
        {
            Reward Reward = _repo.GetById(id);
            if (Reward == null) return NotFound();
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