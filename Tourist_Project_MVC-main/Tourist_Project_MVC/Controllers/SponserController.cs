using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class SponsorController : Controller
    {
        private readonly ISponsorRepository sponsorRepo;
        private readonly IRewardRepository rewardsRepo;

        public SponsorController(ISponsorRepository sponsorRepo, IRewardRepository rewardsRepo)
        {
            this.sponsorRepo = sponsorRepo;
            this.rewardsRepo = rewardsRepo;
        }

        #region Index
        public IActionResult Index(string? search, string? type)
        {
            IEnumerable<Sponsor> allSponsers = sponsorRepo.GetAll();

            //  Filter Bar Data
            ViewBag.AllCount = allSponsers.Count();
            ViewBag.Types = allSponsers
                .Select(s => s.Type)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            ViewBag.Search = search;
            ViewBag.Type = type;


            if (!string.IsNullOrEmpty(search))
                allSponsers = allSponsers.Where(s =>
                    s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Address.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(type))
                allSponsers = allSponsers.Where(s => s.Type == type);

            return View("index", allSponsers);
        }
        #endregion

        #region Details
        public IActionResult Details(int Id)
        {
            Sponsor? sponsorFromDB = sponsorRepo.GetByIdWithRewars(Id);
            if (sponsorFromDB == null)
            {
                return NotFound();
            }
            return View("Details", sponsorFromDB);
        }
        #endregion

        #region Edit(Only_Admin)
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            Sponsor? sponsorFromDB = sponsorRepo.GetById(id);
            IEnumerable<Reward> allRewards = rewardsRepo.GetAll();
            if (sponsorFromDB == null)
            {
                return NotFound();
            }
            sponsorViewModel sponsorViewModel = new sponsorViewModel()
            {
                Id = sponsorFromDB.Id,
                Name = sponsorFromDB.Name,
                Type = sponsorFromDB.Type,
                City = sponsorFromDB.Address,
                ContactInfo = sponsorFromDB.ContactNumber,
            };
            return View("Edit", sponsorViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(sponsorViewModel sponsorFromReq)
        {
            if (ModelState.IsValid)
            {
                Sponsor sponsorFromDB = sponsorRepo.GetById(sponsorFromReq.Id);
                sponsorFromDB.Name = sponsorFromReq.Name;
                sponsorFromDB.Type = sponsorFromReq.Type;
                sponsorFromDB.Address = sponsorFromReq.City;
                sponsorFromDB.ContactNumber = sponsorFromReq.ContactInfo;
                sponsorRepo.Update(sponsorFromDB);
                sponsorRepo.Save();
                return RedirectToAction("Index");
            }
            return RedirectToAction("Edit", sponsorFromReq);
        }
        #endregion

        #region Create(Only_Admin)
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            sponsorViewModel createSponsorModel = new sponsorViewModel();
            return View("Create", createSponsorModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Create(sponsorViewModel sponsorFromReq)
        {
            if (ModelState.IsValid)
            {
                Sponsor newSponsor = new Sponsor()
                {
                    Name = sponsorFromReq.Name,
                    Type = sponsorFromReq.Type,
                    Address = sponsorFromReq.City,
                    ContactNumber = sponsorFromReq.ContactInfo,
                };
                sponsorRepo.Add(newSponsor);
                sponsorRepo.Save();
                return RedirectToAction("Index");
            }
            return RedirectToAction("Create", sponsorFromReq);
        }
        #endregion

        #region Delete(Only_Admin)
        [Authorize(Roles = "Admin")]
        public IActionResult Delete(int id)
        {
            Sponsor sponsorFromDB = sponsorRepo.GetById(id);
            if (sponsorFromDB == null)
            {
                return NotFound();
            }
            sponsorViewModel deletedSponsorVM = new sponsorViewModel()
            {
                Id = sponsorFromDB.Id,
                Name = sponsorFromDB.Name,
                Type = sponsorFromDB.Type,
                City = sponsorFromDB.Address,
                ContactInfo = sponsorFromDB.ContactNumber,
            };
            return View("Delete", deletedSponsorVM);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteConfirmed(int id)
        {
            sponsorRepo.Delete(id);
            sponsorRepo.Save();
            return RedirectToAction("Index");
        }
        #endregion
    }
}