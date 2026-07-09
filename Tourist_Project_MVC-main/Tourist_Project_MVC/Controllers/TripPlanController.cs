using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class TripPlanController : Controller
    {
        private readonly ITripPlanRepository _repo;
        private readonly ITouristRepository _touristRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly TouristContext _context;

        public TripPlanController(
            ITripPlanRepository repo,
            ITouristRepository touristRepo,
            IDestinationRepository destinationRepo,
            TouristContext context)
        {
            _repo = repo;
            _touristRepo = touristRepo;
            _destinationRepo = destinationRepo;
            _context = context;
        }

        public IActionResult Index(string? search, int? touristId)
        {
            var all = _repo.GetAllWithDetails();

            // Filter Bar Data
            ViewBag.AllCount = all.Count();
            ViewBag.Tourists = new SelectList(_touristRepo.GetAll(), "Id", "Name");
            ViewBag.Search = search;
            ViewBag.TouristId = touristId;


            if (!string.IsNullOrEmpty(search))
                all = all.Where(t =>
                    t.Title.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (touristId.HasValue)
                all = all.Where(t => t.TouristId == touristId);

            // Top stat-box row (real aggregates, query-level).
            var now = DateTime.Now;
            var total = _context.TripPlans.Count();
            var startingThisMonth = _context.TripPlans.Count(t =>
                t.StartDate.Year == now.Year && t.StartDate.Month == now.Month);

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-map-fill", Color = "blue", Value = total.ToString("N0"), Label = "Total Trip Plans" },
                new StatBoxItem { IconClass = "bi-check-circle-fill", Color = "green", Value = _context.TripPlans.Count(t => t.Status == "Active").ToString("N0"), Label = "Active Trip Plans" },
                new StatBoxItem { IconClass = "bi-calendar-event-fill", Color = "gold", Value = startingThisMonth.ToString("N0"), Label = "Starting This Month" },
                new StatBoxItem { IconClass = "bi-pin-map-fill", Color = "purple", Value = _context.TripDestinations.Count().ToString("N0"), Label = "Destinations Planned" }
            };

            return View(all);
        }

        public IActionResult Details(int id)
        {
            var trip = _repo.GetByIdWithDetails(id);
            if (trip == null) return NotFound();
            return View(trip);
        }

        public IActionResult Create()
        {
            ViewBag.Tourists = new SelectList(_touristRepo.GetAll(), "Id", "Name");
            ViewBag.Destinations = _destinationRepo.GetAll().ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(TripPlan trip, List<int> SelectedDestinations)
        {
            if (ModelState.IsValid)
            {
                trip.TripDestinations = SelectedDestinations
                    .Select((destId, index) => new TripDestination
                    {
                        DestinationId = destId,
                        Visit_Order = index + 1,
                        ArrivalDate = trip.StartDate,
                        DepartureDate = trip.EndDate
                    }).ToList();

                _repo.Add(trip);
                _repo.Save();
                return RedirectToAction("Index");
            }
            ViewBag.Tourists = new SelectList(_touristRepo.GetAll(), "Id", "Name");
            ViewBag.Destinations = _destinationRepo.GetAll().ToList();
            return View(trip);
        }

        public IActionResult Edit(int id)
        {
            var trip = _repo.GetByIdWithDetails(id);
            if (trip == null) return NotFound();
            ViewBag.Tourists = new SelectList(_touristRepo.GetAll(), "Id", "Name", trip.TouristId);
            ViewBag.Destinations = _destinationRepo.GetAll().ToList();
            ViewBag.SelectedDestinations = trip.TripDestinations
                .Select(td => td.DestinationId).ToList();
            return View(trip);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(TripPlan trip, List<int> SelectedDestinations)
        {
            if (ModelState.IsValid)
            {
                _repo.RemoveTripDestinations(trip.Id);

                trip.TripDestinations = SelectedDestinations
                    .Select((destId, index) => new TripDestination
                    {
                        TripPlanId = trip.Id,
                        DestinationId = destId,
                        Visit_Order = index + 1,
                        ArrivalDate = trip.StartDate,
                        DepartureDate = trip.EndDate
                    }).ToList();

                _repo.Update(trip);
                _repo.Save();
                return RedirectToAction("Index");
            }
            ViewBag.Tourists = new SelectList(_touristRepo.GetAll(), "Id", "Name", trip.TouristId);
            ViewBag.Destinations = _destinationRepo.GetAll().ToList();
            return View(trip);
        }

        public IActionResult Delete(int id)
        {
            var trip = _repo.GetByIdWithDetails(id);
            if (trip == null) return NotFound();
            return View(trip);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            _repo.RemoveTripDestinations(id);
            _repo.Delete(id);
            _repo.Save();
            return RedirectToAction("Index");
        }
    }
}