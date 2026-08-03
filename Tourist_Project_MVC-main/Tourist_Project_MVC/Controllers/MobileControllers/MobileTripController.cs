using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.DTOs;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.Controllers.MobileControllers
{
    // Trip planning for the React Native app. The website's TripController does
    // the same job over cookie auth + antiforgery-protected form posts, which the
    // mobile client can't use; this exposes the same TripPlan/TripDestination
    // model over JWT + JSON.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MobileTripController : ControllerBase
    {
        private readonly ITripPlanRepository _tripPlanRepo;
        private readonly ITouristRepository _touristRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IGamificationService _gamificationService;

        public MobileTripController(
            ITripPlanRepository tripPlanRepository,
            ITouristRepository touristRepository,
            IDestinationRepository destinationRepository,
            UserManager<ApplicationUser> userManager,
            IGamificationService gamificationService)
        {
            _tripPlanRepo = tripPlanRepository;
            _touristRepo = touristRepository;
            _destinationRepo = destinationRepository;
            _userManager = userManager;
            _gamificationService = gamificationService;
        }

        // Npgsql maps DateTime to "timestamp without time zone" and rejects any
        // value whose Kind is Utc. Dates arriving as JSON can carry a Utc kind,
        // so every date is stripped to a bare calendar day before it's persisted
        // (same reason MobileMissionController calls SpecifyKind on Completed_At).
        private static DateTime Normalize(DateTime value) =>
            DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);

        [HttpPost("CreateTrip")]
        public async Task<IActionResult> CreateTrip([FromBody] CreateTripDto dto)
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new { message = "Trip name is required." });
            }
            if (dto.DestinationIds == null || !dto.DestinationIds.Any())
            {
                return BadRequest(new { message = "Select at least one destination for your trip." });
            }
            if (dto.EndDate.Date < dto.StartDate.Date)
            {
                return BadRequest(new { message = "End date must be on or after the start date." });
            }
            if (dto.Budget.HasValue && dto.Budget.Value < 0)
            {
                return BadRequest(new { message = "Budget must be a positive amount." });
            }
            if (dto.Companions.HasValue && (dto.Companions.Value < 1 || dto.Companions.Value > 100))
            {
                return BadRequest(new { message = "Companions must be between 1 and 100." });
            }

            // Preserve the order the app sent while dropping duplicates, then
            // confirm every id is real before writing anything.
            var orderedIds = dto.DestinationIds.Distinct().ToList();
            var known = _destinationRepo.GetAll().Select(d => d.Id).ToHashSet();
            var unknown = orderedIds.Where(id => !known.Contains(id)).ToList();
            if (unknown.Any())
            {
                return BadRequest(new { message = $"Unknown destination(s): {string.Join(", ", unknown)}" });
            }

            var startDate = Normalize(dto.StartDate);
            var endDate = Normalize(dto.EndDate);

            var trip = new TripPlan
            {
                Title = dto.Title.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                Budget = dto.Budget,
                Companions = dto.Companions,
                Status = "Active",
                TouristId = tourist.Id,
                TripDestinations = orderedIds.Select((destinationId, index) => new TripDestination
                {
                    DestinationId = destinationId,
                    Visit_Order = index + 1,
                    // The mobile form only collects a trip-level range, so each
                    // stop gets a one-day window inside it rather than asking the
                    // user for a date pair per destination.
                    ArrivalDate = startDate,
                    DepartureDate = startDate.AddDays(1) > endDate ? endDate : startDate.AddDays(1)
                }).ToList()
            };

            _tripPlanRepo.Add(trip);
            _tripPlanRepo.Save();

            var saved = _tripPlanRepo.GetByIdWithDetails(trip.Id);
            return Ok(ToDetailsDto(saved ?? trip));
        }

        [HttpGet("MyTrips")]
        public async Task<IActionResult> MyTrips()
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            // GetFiltered already includes TripDestinations.Destination and
            // orders by StartDate descending.
            var trips = _tripPlanRepo.GetFiltered(null, tourist.Id)
                .Select(t => new TripSummaryDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Status = t.Status,
                    Budget = t.Budget,
                    Companions = t.Companions,
                    StopCount = t.TripDestinations.Count
                })
                .ToList();

            return Ok(trips);
        }

        [HttpPost("GetTripById")]
        public async Task<IActionResult> GetTripById([FromBody] TripIdDto dto)
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var trip = _tripPlanRepo.GetByIdWithDetails(dto.TripId);
            // NotFound rather than Forbid on an ownership miss: a 403 would tell
            // the caller the id exists.
            if (trip == null || trip.TouristId != tourist.Id)
            {
                return NotFound(new { message = "Trip not found." });
            }

            return Ok(ToDetailsDto(trip));
        }

        [HttpPost("CompleteTrip")]
        public async Task<IActionResult> CompleteTrip([FromBody] TripIdDto dto)
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var trip = _tripPlanRepo.GetByIdWithDetails(dto.TripId);
            if (trip == null || trip.TouristId != tourist.Id)
            {
                return NotFound(new { message = "Trip not found." });
            }
            if (trip.Status == "Completed")
            {
                return Conflict(new { message = "This trip is already marked as completed." });
            }

            trip.Status = "Completed";
            _tripPlanRepo.Update(trip);
            _tripPlanRepo.Save();

            // Same award as the website's TripController.CompleteTrip.
            var (xpAdded, newBadges) = await _gamificationService.AwardXPAsync(tourist.Id, 75, "trip-complete");

            return Ok(new
            {
                message = "Trip completed successfully",
                status = trip.Status,
                xpAdded,
                newBadges = newBadges?.Select(b => new { b.Name, b.Icon }).ToList()
            });
        }

        [HttpPost("DeleteTrip")]
        public async Task<IActionResult> DeleteTrip([FromBody] TripIdDto dto)
        {
            var applicationUser = await _userManager.GetUserAsync(User);
            if (applicationUser == null)
            {
                return Unauthorized();
            }
            var tourist = _touristRepo.GetOrCreateByApplicationUser(applicationUser);

            var trip = _tripPlanRepo.GetByIdWithDetails(dto.TripId);
            if (trip == null || trip.TouristId != tourist.Id)
            {
                return NotFound(new { message = "Trip not found." });
            }

            _tripPlanRepo.RemoveTripDestinations(trip.Id);
            _tripPlanRepo.Delete(trip.Id);
            _tripPlanRepo.Save();

            return Ok(new { message = "Trip deleted successfully", tripId = dto.TripId });
        }

        private static TripDetailsDto ToDetailsDto(TripPlan trip) => new TripDetailsDto
        {
            Id = trip.Id,
            Title = trip.Title,
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,
            Status = trip.Status,
            Budget = trip.Budget,
            Companions = trip.Companions,
            StopCount = trip.TripDestinations.Count,
            Stops = trip.TripDestinations
                .OrderBy(td => td.Visit_Order)
                .Select(td => new TripStopDto
                {
                    StopId = td.Id,
                    DestinationId = td.DestinationId,
                    Name = td.Destination?.Name ?? string.Empty,
                    City = td.Destination?.City ?? string.Empty,
                    Category = td.Destination?.Category,
                    TicketPrice = td.Destination?.TicketPrice,
                    Latitude = td.Destination?.Location != null ? td.Destination.Location.Y : 0,
                    Longitude = td.Destination?.Location != null ? td.Destination.Location.X : 0,
                    VisitOrder = td.Visit_Order,
                    ArrivalDate = td.ArrivalDate,
                    DepartureDate = td.DepartureDate
                })
                .ToList()
        };
    }
}
