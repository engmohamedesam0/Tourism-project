using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    // Tourist-facing discovery page. Read-only browsing of destinations.
    // Guests may browse read-only; Trip planning is gated behind sign-in elsewhere.
    public class ExploreController : Controller
    {
        private readonly IDestinationRepository _repo;

        public ExploreController(IDestinationRepository repo)
        {
            _repo = repo;
        }

        public IActionResult Index(string? search)
        {
            var all = _repo.GetAll();

            if (!string.IsNullOrWhiteSpace(search))
            {
                all = all.Where(d =>
                    d.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (d.City != null && d.City.Contains(search, System.StringComparison.OrdinalIgnoreCase)) ||
                    (d.Description != null && d.Description.Contains(search, System.StringComparison.OrdinalIgnoreCase)));
            }

            ViewBag.Search = search;
            return View(all);
        }
    }
}
