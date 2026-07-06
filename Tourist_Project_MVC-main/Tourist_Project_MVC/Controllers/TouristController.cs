using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    public class TouristController : Controller
    {
        private readonly ITouristRepository _repo;

        public TouristController(ITouristRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(string? search, string? nationality)
        {

            var all = _repo.GetAllWithDetails();

            ViewBag.AllCount = all.Count();
            ViewBag.Nationalities = all
                .Select(t => t.Nationality)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ViewBag.Search = search;
            ViewBag.Nationality = nationality;

            if (!string.IsNullOrEmpty(search))
                all = all.Where(t =>
                    t.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    t.Nationality.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(nationality))
                all = all.Where(t => t.Nationality == nationality);

            return View(all);
        }

        public IActionResult Details(int id)
        {
            var tourist = _repo.GetByIdWithDetails(id);
            if (tourist == null) return NotFound();
            return View(tourist);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Tourist tourist)
        {
            if (ModelState.IsValid)
            {
                tourist.RegisterDate = DateTime.Now;
                tourist.Status = "Active";
                _repo.Add(tourist);
                _repo.Save();
                return RedirectToAction("Index");
            }
            return View(tourist);
        }

        public IActionResult Edit(int id)
        {
            var tourist = _repo.GetById(id);
            if (tourist == null) return NotFound();
            return View(tourist);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Tourist tourist)
        {
            if (ModelState.IsValid)
            {
                _repo.Update(tourist);
                _repo.Save();
                return RedirectToAction("Index");
            }
            return View(tourist);
        }

        public IActionResult Delete(int id)
        {
            var tourist = _repo.GetById(id);
            if (tourist == null) return NotFound();
            return View(tourist);
        }

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