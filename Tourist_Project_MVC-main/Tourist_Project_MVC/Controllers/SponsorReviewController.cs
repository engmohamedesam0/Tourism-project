using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Sponsor")]
    public class SponsorReviewController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly TouristContext _context;

        public SponsorReviewController(ISponsorRepository sponsorRepo, TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _context = context;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        public IActionResult Index(int? ratingFilter)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var sponsorId = sponsor.Id;
            var query = _context.Reviews
                .Include(r => r.Tourist)
                .Where(r => r.SponsorId == sponsorId)
                .AsQueryable();

            if (ratingFilter.HasValue && ratingFilter.Value >= 1 && ratingFilter.Value <= 5)
            {
                query = query.Where(r => r.Rating == ratingFilter.Value);
            }

            var reviews = query
                .OrderByDescending(r => r.CreatedDate)
                .ToList();

            var vm = new SponsorReviewListVM
            {
                SponsorId = sponsorId,
                RatingAvailable = reviews.Any(),
                AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : (double?)null,
                ReviewCount = reviews.Count,
                Reviews = reviews.Select(r => new ReviewListItem
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    TouristName = r.Tourist?.Name ?? "Tourist",
                    CreatedDate = r.CreatedDate
                }).ToList(),
                SelectedRating = ratingFilter
            };

            return View("Index", vm);
        }
    }
}
