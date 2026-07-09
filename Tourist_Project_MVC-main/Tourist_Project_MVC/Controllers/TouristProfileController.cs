using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User")]
    public class TouristProfileController : Controller
    {
        private readonly ITouristRepository _touristRepo;
        private readonly TouristContext _context;

        public TouristProfileController(ITouristRepository touristRepo, TouristContext context)
        {
            _touristRepo = touristRepo;
            _context = context;
        }

        public IActionResult Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists
                .FirstOrDefault(t => t.ApplicationUserId == userId);

            if (tourist == null)
            {
                var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
                tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
            }

            var appUserDetails = _context.Users
                .FirstOrDefault(u => u.Id == tourist.ApplicationUserId);

            var vm = new TouristProfileVM
            {
                Id = tourist.Id,
                Name = tourist.Name,
                FirstName = appUserDetails?.FirstName,
                LastName = appUserDetails?.LastName,
                Email = tourist.Email ?? appUserDetails?.Email ?? string.Empty,
                PhoneNumber = appUserDetails?.PhoneNumber,
                Nationality = tourist.Nationality,
                RegisterDate = tourist.RegisterDate,
                Status = tourist.Status,
                PointBalance = tourist.point_Balance,
                ProfilePicturePath = appUserDetails?.ProfilePicturePath
            };

            return View(vm);
        }
    }
}
