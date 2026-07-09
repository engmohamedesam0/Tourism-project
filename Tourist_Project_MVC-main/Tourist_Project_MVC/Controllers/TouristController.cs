using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class TouristController : Controller
    {
        private readonly ITouristRepository _repo;
        private readonly TouristContext _context;

        public TouristController(ITouristRepository repo, TouristContext context)
        {
            _repo = repo;
            _context = context;
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

            // Top stat-box row (real aggregates, query-level counts).
            // "Active Today" isn't tracked (no last-activity field on Tourist),
            // so we surface "Active Accounts" (Status = Active) instead; and
            // "Retention Rate" isn't computable, so we show "% Active".
            var now = DateTime.Now;
            var total = _context.Tourists.Count();
            var newThisMonth = _context.Tourists.Count(t =>
                t.RegisterDate.Year == now.Year && t.RegisterDate.Month == now.Month);
            var active = _context.Tourists.Count(t => t.Status == "Active");
            var pctActive = total > 0 ? Math.Round(active * 100.0 / total) : 0;

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-people-fill", Color = "blue",  Value = total.ToString("N0"),        Label = "Total Tourists" },
                new StatBoxItem { IconClass = "bi-person-plus-fill", Color = "green", Value = newThisMonth.ToString("N0"), Label = "New This Month" },
                new StatBoxItem { IconClass = "bi-person-check-fill", Color = "gold", Value = active.ToString("N0"),  Label = "Active Accounts" },
                new StatBoxItem { IconClass = "bi-graph-up", Color = "purple", Value = pctActive.ToString("N0") + "%", Label = "% Active" }
            };

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