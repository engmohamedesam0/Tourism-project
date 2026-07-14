using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
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
        public async Task<IActionResult> Index(int? destinationId, string? search, string? type, string? sort, string? distance, string? rating)
        {
            var destinations = _destinationRepo.GetAll().ToList();
            var selectedDest = destinationId.HasValue
                ? destinations.FirstOrDefault(d => d.Id == destinationId.Value) ?? destinations.FirstOrDefault()
                : destinations.FirstOrDefault();

            var origin = selectedDest?.Location != null
                ? new Point(selectedDest.Location.X, selectedDest.Location.Y) { SRID = 4326 }
                : new Point(0, 0) { SRID = 4326 };

            // Real spatial proximity: nearest-branch distance per sponsor via
            // PostGIS ST_Distance on geography (meter-accurate, not Haversine).
            var proximity = await _context.Database
                .SqlQuery<BranchProximity>(@$"
                    SELECT s.""Id"" AS SponsorId,
                           MIN(ST_Distance(b.""Location""::geography, {origin}::geography) / 1000.0) AS DistanceKm,
                           (ARRAY_AGG(b.""Id"" ORDER BY ST_Distance(b.""Location""::geography, {origin}::geography)))[1] AS NearestBranchId
                    FROM ""Sponsors"" s
                    JOIN ""Branches"" b ON b.""SponsorId"" = s.""Id""
                    GROUP BY s.""Id""")
                .ToListAsync();
            var proximityById = proximity.ToDictionary(p => p.SponsorId);

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
                var prox = proximityById.TryGetValue(s.Id, out var p) ? p : null;
                var nearest = (s.Branches != null && prox != null)
                    ? s.Branches.FirstOrDefault(b => b.Id == prox.NearestBranchId) ?? s.Branches.FirstOrDefault()
                    : s.Branches?.FirstOrDefault();
                double sLat = nearest?.Location.Y ?? 0;
                double sLong = nearest?.Location.X ?? 0;

                return new SponsorCardVM
                {
                    Sponsor = s,
                    Lat = sLat,
                    Long = sLong,
                    DistanceKm = prox?.DistanceKm ?? 0,
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

            var firstSponsor = vm.Cards.FirstOrDefault()?.Sponsor;
            if (firstSponsor != null)
            {
                var sponsorReviews = _context.Reviews
                    .Include(r => r.Tourist)
                    .Where(r => r.SponsorId == firstSponsor.Id)
                    .OrderByDescending(r => r.CreatedDate)
                    .Take(5)
                    .ToList();

                ViewBag.NearMeCarousel = new Tourist_Project_MVC.View_Model.ReviewsCarouselVM
                {
                    Title = "Traveler Reviews",
                    TargetTitle = firstSponsor.Name,
                    Items = sponsorReviews.Select(r => new Tourist_Project_MVC.View_Model.ReviewsCarouselItemVM
                    {
                        TouristName = r.Tourist?.Name ?? "Tourist",
                        TouristPhotoPath = null,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedDate = r.CreatedDate
                    }).ToList(),
                    CanAddReview = false,
                    TargetId = firstSponsor.Id,
                    TargetType = "Sponsor"
                };
            }

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

        // Per-sponsor nearest-branch proximity result from the spatial query.
        private sealed record BranchProximity
        {
            public int SponsorId { get; set; }
            public double DistanceKm { get; set; }
            public int NearestBranchId { get; set; }
        }
    }
}
