using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Tourist-facing trip planner. Distinct from the admin TripPlan CRUD:
    // the signed-in user builds a plan tied to THEIR OWN Tourist record.
    [Authorize(Roles = "User")]
    public class TripController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly ITripPlanRepository _tripPlanRepo;

        public TripController(
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IDestinationRepository destinationRepo,
            ITripPlanRepository tripPlanRepo)
        {
            _userManager = userManager;
            _touristRepo = touristRepo;
            _destinationRepo = destinationRepo;
            _tripPlanRepo = tripPlanRepo;
        }

        // Resolve the signed-in ApplicationUser to a Tourist record.
        // Uses the centralized resolver in ITouristRepository, which links by FK,
        // falls back to email (and persists the link), or auto-creates — never null.
        private Tourist ResolveTourist()
        {
            var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            return _touristRepo.GetOrCreateByApplicationUser(user);
        }

        public IActionResult Index()
        {
            var tourist = ResolveTourist();

            // Existing trip plans for this tourist (timeline above the builder).
            var myTrips = tourist == null
                ? new List<TripPlan>()
                : _tripPlanRepo.GetAllWithDetails()
                    .Where(t => t.TouristId == tourist.Id)
                    .OrderByDescending(t => t.StartDate)
                    .ToList();

            ViewBag.Tourist = tourist;
            ViewBag.MyTrips = myTrips;

            // Build the picker from all available destinations.
            var vm = new TripBuilderVM
            {
                Stops = _destinationRepo.GetAll().Select(d => new TripStopVM
                {
                    DestinationId = d.Id,
                    DestinationName = d.Name,
                    City = d.City,
                    ArrivalDate = DateTime.Today,
                    DepartureDate = DateTime.Today.AddDays(1)
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(TripBuilderVM vm)
        {
            var tourist = ResolveTourist();

            var selected = vm.Stops.Where(s => s.Selected).ToList();
            if (!selected.Any())
            {
                ModelState.AddModelError("", "Select at least one destination for your trip.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Tourist = tourist;
                ViewBag.MyTrips = _tripPlanRepo.GetAllWithDetails().Where(t => t.TouristId == tourist.Id);
                return View("Index", vm);
            }

            var trip = new TripPlan
            {
                Title = vm.Title,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                Status = "Active",
                TouristId = tourist.Id,
                TripDestinations = selected.Select((s, index) => new TripDestination
                {
                    DestinationId = s.DestinationId,
                    Visit_Order = index + 1,
                    ArrivalDate = s.ArrivalDate,
                    DepartureDate = s.DepartureDate
                }).ToList()
            };

            _tripPlanRepo.Add(trip);
            _tripPlanRepo.Save();

            return RedirectToAction("Index");
        }
    }
}
