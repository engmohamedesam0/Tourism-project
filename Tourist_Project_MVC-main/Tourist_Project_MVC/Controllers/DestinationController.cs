using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    public class DestinationController : Controller
    {
        private readonly IDestinationRepository _repo;

        public DestinationController(IDestinationRepository repo)
        {
            _repo = repo;
        }

        // GET: /Destination/Index
        public IActionResult Index(string? search, string? status, string? category)
        {
            var all = _repo.GetAll();

            ViewBag.AllCount = all.Count();
            ViewBag.ActiveCount = all.Count(d => d.Status == "Active");
            ViewBag.PendingCount = all.Count(d => d.Status == "Pending");
            ViewBag.InactiveCount = all.Count(d => d.Status == "Inactive");

            ViewBag.Categories = all
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .ToList();

            var destinations = _repo.GetFiltered(search, status, category);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Category = category;

            return View(destinations);
        }

        // GET: /Destination/Details
        public IActionResult Details(int id)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();

            destination.Visits++;
            _repo.Update(destination);
            _repo.Save();

            // Context-aware back target: respect the referrer so tourists return
            // to Explore (filters/scroll intact) and admins return to the admin list.
            var referrer = Request.Headers["Referer"].ToString();
            string backUrl = "/Destination";
            if (!string.IsNullOrEmpty(referrer))
            {
                if (referrer.Contains("/Explore", System.StringComparison.OrdinalIgnoreCase))
                    backUrl = "/Explore";
                else if (referrer.Contains("/Trip", System.StringComparison.OrdinalIgnoreCase))
                    backUrl = "/Trip";
            }
            ViewBag.BackUrl = backUrl;

            return View(destination);
        }

        // GET: /Destination/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Destination/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Destination destination)
        {
            if (ModelState.IsValid)
            {
                destination.Visits = 0;
                _repo.Add(destination);
                _repo.Save();
                return RedirectToAction("Index");
            }
            return View(destination);
        }

        // GET: /Destination/Edit/5
        public IActionResult Edit(int id)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();
            return View(destination);
        }

        // POST: /Destination/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Destination destination)
        {
            if (ModelState.IsValid)
            {
                _repo.Update(destination);
                _repo.Save();
                return RedirectToAction("Index");
            }
            return View(destination);
        }

        // GET: /Destination/Delete
        public IActionResult Delete(int id)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();
            return View(destination);
        }

        // POST: /Destination/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.Delete(id);
            _repo.Save();
            return RedirectToAction("Index");
        }
    }
}