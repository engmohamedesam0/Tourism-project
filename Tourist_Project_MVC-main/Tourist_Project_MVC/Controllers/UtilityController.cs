using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    // Public directory of essential utilities in Egypt (Police Stations,
    // Fire Stations, Hospitals, Pharmacies). Browsable by everyone; the
    // data itself is managed by Admins.
    public class UtilityController : Controller
    {
        private readonly TouristContext _context;

        public UtilityController(TouristContext context)
        {
            _context = context;
        }

        #region Index (public)

        public IActionResult Index(string? type, string? search)
        {
            var all = _context.Utilities.AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                all = all.Where(u => u.Type == type);

            if (!string.IsNullOrWhiteSpace(search))
                all = all.Where(u =>
                    u.Name.Contains(search) ||
                    (u.City != null && u.City.Contains(search)) ||
                    (u.Address != null && u.Address.Contains(search)));

            var utilities = all.OrderBy(u => u.Type).ThenBy(u => u.Name).ToList();

            ViewBag.AllCount = _context.Utilities.Count();
            ViewBag.Types = UtilityTypes.All.ToList();
            ViewBag.Type = type;
            ViewBag.Search = search;

            // Top stat-box row (real aggregates).
            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-shield-fill", Color = "blue", Value = utilities.Count(u => u.Type == "Police Station").ToString("N0"), Label = "Police Stations" },
                new StatBoxItem { IconClass = "bi-fire", Color = "red", Value = utilities.Count(u => u.Type == "Fire Station").ToString("N0"), Label = "Fire Stations" },
                new StatBoxItem { IconClass = "bi-heart-pulse-fill", Color = "green", Value = utilities.Count(u => u.Type == "Hospital").ToString("N0"), Label = "Hospitals" },
                new StatBoxItem { IconClass = "bi-capsule-fill", Color = "gold", Value = utilities.Count(u => u.Type == "Pharmacy").ToString("N0"), Label = "Pharmacies" }
            };

            return View("Index", utilities);
        }

        #endregion

        #region Create (Admin only)

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View("Create", new UtilityVM { Lat = 30.0444f, Long = 31.2357f });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Create(UtilityVM vm)
        {
            if (ModelState.IsValid)
            {
                if (!UtilityTypes.IsValid(vm.Type))
                {
                    ModelState.AddModelError("Type", "Please choose a valid type from the list.");
                    return View("Create", vm);
                }

                var utility = new Utility
                {
                    Name = vm.Name,
                    Type = vm.Type,
                    Address = vm.Address,
                    City = vm.City,
                    ContactNumber = vm.ContactNumber,
                    OpenHours = vm.OpenHours,
                    Location = new Point(vm.Long, vm.Lat) { SRID = 4326 }
                };

                _context.Utilities.Add(utility);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View("Create", vm);
        }

        #endregion

        #region Edit (Admin only)

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            var utility = _context.Utilities.FirstOrDefault(u => u.Id == id);
            if (utility == null) return NotFound();

            var vm = new UtilityVM
            {
                Id = utility.Id,
                Name = utility.Name,
                Type = utility.Type,
                Address = utility.Address,
                City = utility.City,
                ContactNumber = utility.ContactNumber,
                OpenHours = utility.OpenHours,
                Lat = (float)utility.Location.Y,
                Long = (float)utility.Location.X
            };
            return View("Edit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(UtilityVM vm)
        {
            if (ModelState.IsValid)
            {
                if (!UtilityTypes.IsValid(vm.Type))
                {
                    ModelState.AddModelError("Type", "Please choose a valid type from the list.");
                    return View("Edit", vm);
                }

                var utility = _context.Utilities.FirstOrDefault(u => u.Id == vm.Id);
                if (utility == null) return NotFound();

                utility.Name = vm.Name;
                utility.Type = vm.Type;
                utility.Address = vm.Address;
                utility.City = vm.City;
                utility.ContactNumber = vm.ContactNumber;
                utility.OpenHours = vm.OpenHours;
                utility.Location = new Point(vm.Long, vm.Lat) { SRID = 4326 };

                _context.Utilities.Update(utility);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View("Edit", vm);
        }

        #endregion

        #region Delete (Admin only)

        [Authorize(Roles = "Admin")]
        public IActionResult Delete(int id)
        {
            var utility = _context.Utilities.FirstOrDefault(u => u.Id == id);
            if (utility == null) return NotFound();
            return View("Delete", utility);
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteConfirmed(int id)
        {
            var utility = _context.Utilities.FirstOrDefault(u => u.Id == id);
            if (utility == null) return NotFound();

            _context.Utilities.Remove(utility);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        #endregion
    }
}
