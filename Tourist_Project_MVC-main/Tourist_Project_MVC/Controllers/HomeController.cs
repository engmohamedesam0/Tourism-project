using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITouristRepository _touristRepo;
        private readonly IDestinationRepository _destinationRepo;
        private readonly ITripPlanRepository _tripPlanRepo;
        private readonly IMissionRepository _missionRepo;

        public HomeController(
            ITouristRepository touristRepo,
            IDestinationRepository destinationRepo,
            ITripPlanRepository tripPlanRepo,
            IMissionRepository missionRepo)
        {
            _touristRepo = touristRepo;
            _destinationRepo = destinationRepo;
            _tripPlanRepo = tripPlanRepo;
            _missionRepo = missionRepo;
        }

        public IActionResult Index()
        {
            ViewBag.TouristCount = _touristRepo.GetAll().Count();
            ViewBag.DestinationCount = _destinationRepo.GetAll().Count();
            ViewBag.TripPlanCount = _tripPlanRepo.GetAll().Count();
            ViewBag.MissionCount = _missionRepo.GetAll().Count();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}