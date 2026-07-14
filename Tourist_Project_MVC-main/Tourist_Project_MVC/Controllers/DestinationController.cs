using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class DestinationController : Controller
    {
        private readonly IDestinationRepository _repo;
        private readonly TouristContext _context;

        public DestinationController(IDestinationRepository repo, TouristContext context)
        {
            _repo = repo;
            _context = context;
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

            // Top stat-box row (real aggregates, query-level).
            var totalVisits = _context.Destinations.Sum(d => (int?)d.Visits) ?? 0;
            var topCategory = _context.Destinations
                .Where(d => d.Category != null)
                .GroupBy(d => d.Category)
                .Select(g => new { Cat = g.Key, Visits = g.Sum(d => d.Visits) })
                .OrderByDescending(g => g.Visits)
                .Select(g => g.Cat)
                .FirstOrDefault() ?? "—";

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-geo-alt-fill", Color = "blue", Value = all.Count().ToString("N0"), Label = "Total Destinations" },
                new StatBoxItem { IconClass = "bi-eye-fill", Color = "green", Value = totalVisits.ToString("N0"), Label = "Total Visits" },
                new StatBoxItem { IconClass = "bi-check-circle-fill", Color = "gold", Value = all.Count(d => d.Status == "Active").ToString("N0"), Label = "Active Destinations" },
                new StatBoxItem { IconClass = "bi-bar-chart-fill", Color = "purple", Value = topCategory, Label = "Top Category (by Visits)" }
            };

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

            var reviews = _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.DestinationId == id)
                .OrderByDescending(r => r.CreatedDate)
                .Take(5)
                .ToList();

            ViewBag.ReviewsCarousel = new Tourist_Project_MVC.View_Model.ReviewsCarouselVM
            {
                Title = "Traveler Reviews",
                TargetTitle = destination.Name,
                Items = reviews.Select(r => new Tourist_Project_MVC.View_Model.ReviewsCarouselItemVM
                {
                    TouristName = r.Tourist?.Name ?? "Tourist",
                    TouristPhotoPath = r.Tourist?.ApplicationUser != null ? r.Tourist.ApplicationUser.ProfilePicturePath : null,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                }).ToList(),
                CanAddReview = User.IsInRole("User"),
                TargetId = destination.Id,
                TargetType = "Destination"
            };

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
        public IActionResult Create(Destination destination, [Range(-90, 90)] double Lat, [Range(-180, 180)] double Long)
        {
            // Build the spatial point from the separate Lat/Long inputs BEFORE validation,
            // otherwise the implicit [Required] on the non-nullable Location property
            // ("The Location field is required") blocks every submit.
            destination.Location = new Point(Long, Lat) { SRID = 4326 };
            ModelState.Remove("Location");

            if (ModelState.IsValid)
            {
                destination.Visits = 0;
                _repo.Add(destination);
                _repo.Save();
                return RedirectToAction("Index");
            }
            ViewBag.Lat = Lat;
            ViewBag.Long = Long;
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
        public IActionResult Edit(Destination destination, [Range(-90, 90)] double Lat, [Range(-180, 180)] double Long)
        {
            // Build the spatial point from the separate Lat/Long inputs BEFORE validation,
            // otherwise the implicit [Required] on the non-nullable Location property
            // ("The Location field is required") blocks every submit.
            destination.Location = new Point(Long, Lat) { SRID = 4326 };
            ModelState.Remove("Location");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(int id, [Bind("Rating,Comment")] SiteReview vm)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();

            if (ModelState.IsValid)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
                if (tourist == null) return RedirectToAction("Index");

                var review = new SiteReview
                {
                    Rating = vm.Rating,
                    Comment = vm.Comment,
                    DestinationId = id,
                    TouristId = tourist.Id,
                    CreatedDate = DateTime.Now
                };

                _context.SiteReviews.Add(review);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }
    }
}