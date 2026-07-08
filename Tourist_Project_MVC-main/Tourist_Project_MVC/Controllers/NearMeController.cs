using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Public sponsor-discovery page. Browsable by guests; only posting a
    // review requires the "User" role. Uses TouristContext directly for
    // Sponsor/Review/MenuItem (no ISponsorRepository), and
    // IDestinationRepository for the destination picker.
    public class NearMeController : Controller
    {
        private readonly TouristContext _context;
        private readonly IDestinationRepository _destinationRepo;
        private readonly ITouristRepository _touristRepo;
        private readonly UserManager<ApplicationUser> _userManager;

        public NearMeController(
            TouristContext context,
            IDestinationRepository destinationRepo,
            ITouristRepository touristRepo,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _destinationRepo = destinationRepo;
            _touristRepo = touristRepo;
            _userManager = userManager;
        }

        [AllowAnonymous]
        public IActionResult Index(int? destinationId, string? search, string? type, string? sort, string? distance, string? rating)
        {
            var destinations = _destinationRepo.GetAll().ToList();
            var selectedDest = destinationId.HasValue
                ? destinations.FirstOrDefault(d => d.Id == destinationId.Value) ?? destinations.FirstOrDefault()
                : destinations.FirstOrDefault();

            double originLat = selectedDest?.Lat ?? 0;
            double originLong = selectedDest?.Long ?? 0;

            var sponsors = _context.Sponsors
                .Include(s => s.Rewards)
                .Include(s => s.MenuItems)
                .Include(s => s.Reviews)
                .Include(s => s.Branches)
                .AsEnumerable();

            // Search by name
            if (!string.IsNullOrWhiteSpace(search))
            {
                sponsors = sponsors.Where(s =>
                    s.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (s.Type != null && s.Type.Contains(search, System.StringComparison.OrdinalIgnoreCase)));
            }

            // Filter by type
            if (!string.IsNullOrWhiteSpace(type))
            {
                sponsors = sponsors.Where(s => s.Type == type);
            }

            // Filter by minimum avg rating
            if (!string.IsNullOrWhiteSpace(rating) && int.TryParse(rating, out int minRating) && minRating > 0)
            {
                sponsors = sponsors.Where(s =>
                    (s.Reviews != null && s.Reviews.Any())
                        ? s.Reviews.Average(r => r.Rating) >= minRating
                        : false);
            }

            // Build cards with computed distance + avg rating
            var cards = sponsors.Select(s =>
            {
                var avg = s.Reviews != null && s.Reviews.Any()
                    ? s.Reviews.Average(r => r.Rating)
                    : 0;

                // A sponsor may have several branches; use the nearest one to the
                // reference destination for the card's distance + map coordinates.
                var nearest = s.Branches != null && s.Branches.Any()
                    ? s.Branches
                        .OrderBy(b => Haversine(originLat, originLong, b.Lat, b.Long))
                        .First()
                    : null;
                double sLat = nearest?.Lat ?? 0;
                double sLong = nearest?.Long ?? 0;

                return new SponsorCardVM
                {
                    Sponsor = s,
                    Lat = sLat,
                    Long = sLong,
                    DistanceKm = Haversine(originLat, originLong, sLat, sLong),
                    AvgRating = avg,
                    ReviewCount = s.Reviews?.Count ?? 0
                };
            }).ToList();

            // Filter by distance
            if (!string.IsNullOrWhiteSpace(distance) &&
                int.TryParse(distance, out int maxKm) && maxKm > 0)
            {
                cards = cards.Where(c => c.DistanceKm <= maxKm).ToList();
            }

            // Sort
            if (sort == "rating")
            {
                cards = cards.OrderByDescending(c => c.AvgRating).ThenBy(c => c.DistanceKm).ToList();
            }
            else
            {
                cards = cards.OrderBy(c => c.DistanceKm).ToList();
            }

            var vm = new NearMeIndexVM
            {
                DestinationId = selectedDest?.Id,
                DestinationName = selectedDest?.Name,
                Destinations = destinations
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = $"{d.Name} ({d.City})",
                        Selected = d.Id == (selectedDest?.Id ?? 0)
                    }).ToList(),
                Search = search,
                Type = type,
                Sort = sort,
                Distance = distance,
                Rating = rating,
                Cards = cards
            };

            return View(vm);
        }

        [AllowAnonymous]
        public IActionResult Details(int id)
        {
            var sponsor = _context.Sponsors
                .Include(s => s.Rewards)
                .Include(s => s.MenuItems)
                .Include(s => s.Reviews)
                    .ThenInclude(r => r.Tourist)
                .Include(s => s.Branches)
                .FirstOrDefault(s => s.Id == id);

            if (sponsor == null)
            {
                return NotFound();
            }

            return View(sponsor);
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(int id, Review vm)
        {
            var sponsor = _context.Sponsors.FirstOrDefault(s => s.Id == id);
            if (sponsor == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var user = _userManager.GetUserAsync(User).Result;
                var tourist = _touristRepo.GetOrCreateByApplicationUser(user!);

                var review = new Review
                {
                    Rating = vm.Rating,
                    Comment = vm.Comment,
                    SponsorId = id,
                    TouristId = tourist.Id,
                    CreatedDate = DateTime.Now
                };

                _context.Reviews.Add(review);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }

        // Pure C# Haversine distance (km). Coords are float -> cast to double.
        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371.0; // Earth radius (km)
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
