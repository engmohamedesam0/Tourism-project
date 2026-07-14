using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
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
        private readonly TouristContext _context;

        public TripController(
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo,
            IDestinationRepository destinationRepo,
            ITripPlanRepository tripPlanRepo,
            TouristContext context)
        {
            _userManager = userManager;
            _touristRepo = touristRepo;
            _destinationRepo = destinationRepo;
            _tripPlanRepo = tripPlanRepo;
            _context = context;
        }

        // Resolve the signed-in ApplicationUser to a Tourist record.
        // Uses the centralized resolver in ITouristRepository, which links by FK,
        // falls back to email (and persists the link), or auto-creates — never null.
        private Tourist ResolveTourist()
        {
            var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            return _touristRepo.GetOrCreateByApplicationUser(user);
        }

        // Get the tourist's single Draft trip, or create one.
        private TripPlan GetOrCreateDraftTrip(int touristId)
        {
            var draft = _tripPlanRepo.GetDraftTrip(touristId);
            if (draft != null) return draft;

            draft = new TripPlan
            {
                Title = "My Trip",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                Status = "Draft",
                TouristId = touristId
            };
            _tripPlanRepo.Add(draft);
            _tripPlanRepo.Save();
            return draft;
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
                    Category = d.Category,
                    Status = d.Status,
                    TicketPrice = d.TicketPrice,
                    ArrivalDate = DateTime.Today,
                    DepartureDate = DateTime.Today.AddDays(1)
                }).ToList()
            };

            // Pre-populate the builder from the active Draft trip, if any.
            var draft = tourist == null ? null : _tripPlanRepo.GetDraftTrip(tourist.Id);
            if (draft != null)
            {
                ViewBag.DraftTripId = draft.Id;
                vm.Title = draft.Title;
                vm.StartDate = draft.StartDate;
                vm.EndDate = draft.EndDate;
                vm.Budget = draft.Budget;
                vm.Companions = draft.Companions;

                var draftStops = draft.TripDestinations.ToDictionary(td => td.DestinationId);
                foreach (var stop in vm.Stops)
                {
                    if (draftStops.TryGetValue(stop.DestinationId, out var td))
                    {
                        stop.Selected = true;
                        stop.ArrivalDate = td.ArrivalDate;
                        stop.DepartureDate = td.DepartureDate;
                    }
                }
            }

            return View(vm);
        }

        // POST: add a destination to the tourist's current Draft trip, then land on the builder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToTrip(int id)
        {
            var tourist = ResolveTourist();
            var destination = _destinationRepo.GetById(id);
            if (destination == null) return NotFound();

            var draft = GetOrCreateDraftTrip(tourist.Id);

            if (!draft.TripDestinations.Any(td => td.DestinationId == id))
            {
                var maxOrder = draft.TripDestinations.Any()
                    ? draft.TripDestinations.Max(td => td.Visit_Order)
                    : 0;

                draft.TripDestinations.Add(new TripDestination
                {
                    DestinationId = id,
                    Visit_Order = maxOrder + 1,
                    ArrivalDate = draft.StartDate,
                    DepartureDate = draft.StartDate.AddDays(1)
                });
                _tripPlanRepo.Update(draft);
                _tripPlanRepo.Save();
            }

            return RedirectToAction("Index");
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

            var draft = _tripPlanRepo.GetDraftTrip(tourist.Id);

            if (draft != null)
            {
                // Finalize the existing Draft → Active, replacing its stops.
                _tripPlanRepo.RemoveTripDestinations(draft.Id);
                draft.Title = vm.Title;
                draft.StartDate = vm.StartDate;
                draft.EndDate = vm.EndDate;
                draft.Budget = vm.Budget;
                draft.Companions = vm.Companions;
                draft.Status = "Active";
                draft.TripDestinations.Clear();
                foreach (var s in selected.Select((stop, index) => new { stop, index }))
                {
                    draft.TripDestinations.Add(new TripDestination
                    {
                        DestinationId = s.stop.DestinationId,
                        Visit_Order = s.index + 1,
                        ArrivalDate = s.stop.ArrivalDate,
                        DepartureDate = s.stop.DepartureDate
                    });
                }
                _tripPlanRepo.Update(draft);
                _tripPlanRepo.Save();
            }
            else
            {
                var trip = new TripPlan
                {
                    Title = vm.Title,
                    StartDate = vm.StartDate,
                    EndDate = vm.EndDate,
                    Budget = vm.Budget,
                    Companions = vm.Companions,
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
            }

            return RedirectToAction("Index");
        }

        // GET: /Trip/Details/{id} — owner-only trip details with reorder/edit/delete.
        [Authorize(Roles = "User")]
        public IActionResult Details(int id)
        {
            var tourist = ResolveTourist();
            var trip = _tripPlanRepo.GetByIdWithDetails(id);

            if (trip == null || trip.TouristId != tourist.Id)
                return Forbid();

            var reviews = _context.SiteReviews
                .Include(r => r.Tourist)
                .Where(r => r.TripPlanId == id)
                .OrderByDescending(r => r.CreatedDate)
                .Take(5)
                .ToList();

            ViewBag.ReviewsCarousel = new Tourist_Project_MVC.View_Model.ReviewsCarouselVM
            {
                Title = "Traveler Reviews",
                TargetTitle = trip.Title,
                Items = reviews.Select(r => new Tourist_Project_MVC.View_Model.ReviewsCarouselItemVM
                {
                    TouristName = r.Tourist?.Name ?? "Tourist",
                    TouristPhotoPath = r.Tourist?.ApplicationUser != null ? r.Tourist.ApplicationUser.ProfilePicturePath : null,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedDate = r.CreatedDate
                }).ToList(),
                CanAddReview = true,
                TargetId = trip.Id,
                TargetType = "TripPlan"
            };

            return View(trip);
        }

        // POST: reorder stops of the owner's trip.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReorderStops(int id, [FromBody] List<int> orderedStopIds)
        {
            var tourist = ResolveTourist();
            var trip = _tripPlanRepo.GetByIdWithDetails(id);
            if (trip == null || trip.TouristId != tourist.Id)
                return Forbid();

            if (orderedStopIds == null)
                return BadRequest();

            foreach (var stopId in orderedStopIds)
            {
                var stop = _tripPlanRepo.GetTripDestination(stopId);
                if (stop == null || stop.TripPlanId != id)
                    return BadRequest();
            }

            for (var i = 0; i < orderedStopIds.Count; i++)
            {
                var stop = _tripPlanRepo.GetTripDestination(orderedStopIds[i]);
                if (stop != null)
                {
                    stop.Visit_Order = i + 1;
                    _tripPlanRepo.UpdateStop(stop);
                }
            }
            _tripPlanRepo.Save();

            return Json(new { success = true });
        }

        // POST: update arrival/departure of a single stop (owner-only).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStopDates(int stopId, DateTime arrival, DateTime departure)
        {
            var tourist = ResolveTourist();
            var stop = _tripPlanRepo.GetTripDestination(stopId);
            if (stop == null)
                return NotFound();

            var trip = _tripPlanRepo.GetByIdWithDetails(stop.TripPlanId);
            if (trip == null || trip.TouristId != tourist.Id)
                return Forbid();

            stop.ArrivalDate = arrival;
            stop.DepartureDate = departure;
            _tripPlanRepo.UpdateStop(stop);
            _tripPlanRepo.Save();

            return Json(new { success = true });
        }

        // POST: remove a single stop from the owner's trip.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStop(int stopId)
        {
            var tourist = ResolveTourist();
            var stop = _tripPlanRepo.GetTripDestination(stopId);
            if (stop == null)
                return NotFound();

            var trip = _tripPlanRepo.GetByIdWithDetails(stop.TripPlanId);
            if (trip == null || trip.TouristId != tourist.Id)
                return Forbid();

            _tripPlanRepo.RemoveStop(stopId);
            _tripPlanRepo.Save();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(int id, [Bind("Rating,Comment")] SiteReview vm)
        {
            var trip = _tripPlanRepo.GetById(id);
            if (trip == null) return NotFound();

            var tourist = ResolveTourist();
            if (trip.TouristId != tourist.Id)
                return Forbid();

            if (ModelState.IsValid)
            {
                var review = new SiteReview
                {
                    Rating = vm.Rating,
                    Comment = vm.Comment,
                    TripPlanId = id,
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
