using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("AdminDashboard")]
    public class AdminDashboardController : Controller
    {
        private readonly TouristContext _context;
        private readonly IArcGISSyncService _arcgisSync;
        private readonly IConfiguration _config;

        public AdminDashboardController(TouristContext context, IArcGISSyncService arcgisSync, IConfiguration config)
        {
            _context = context;
            _arcgisSync = arcgisSync;
            _config = config;
        }

        // -----------------------------------------------------------------------
        // Admin Dashboard -> ArcGIS Online Dashboard (Experience Builder)
        // The admin dashboard now renders ONLY the ArcGIS dashboard pointed to by
        // ArcGIS:DashboardUrl in appsettings.json. The legacy custom chart/table
        // sections were removed in favour of the ArcGIS embed.
        // The {section} route is kept so any stale links (e.g. from the admin
        // module toggle on data pages) still land on the dashboard without 404s.
        // -----------------------------------------------------------------------

        [HttpGet("")]
        [HttpGet("{section}")]
        public IActionResult Index()
        {
            var vm = new AdminDashboardVM
            {
                ArcGISDashboardUrl = _config["ArcGIS:DashboardUrl"]?.Trim() ?? string.Empty
            };
            return View("Index", vm);
        }

        // -----------------------------------------------------------------------
        // ArcGIS On-Demand Sync Actions
        // -----------------------------------------------------------------------

        /// <summary>
        /// POST /AdminDashboard/SyncToArcGIS
        /// Pushes all local destinations (and branches) to the ArcGIS feature layers.
        /// Admins can trigger this manually whenever they add/update destinations locally.
        /// </summary>
        [HttpPost("SyncToArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncToArcGIS()
        {
            var destinations = await _context.Destinations
                .Where(d => d.Location != null)
                .ToListAsync();

            var branches = await _context.Branches
                .Where(b => b.Location != null)
                .ToListAsync();

            var destResult = await _arcgisSync.SyncDestinationsAsync(destinations);
            var branchResult = await _arcgisSync.SyncBranchesAsync(branches);
            var touristsTableResult = await _arcgisSync.SyncTouristsTableAsync();
            var touristNatResult = await _arcgisSync.SyncTouristNationalityLayerAsync();

            if (destResult.Success && branchResult.Success && touristsTableResult.Success && touristNatResult.Success)
            {
                TempData["ArcGISMessage"] = $"✅ Pushed to ArcGIS — {destResult.AddedCount} destinations added, " +
                    $"{destResult.UpdatedCount} updated; {branchResult.AddedCount} branches added, {branchResult.UpdatedCount} updated; " +
                    $"tourists table: {touristsTableResult.AddedCount} added, {touristsTableResult.UpdatedCount} updated; " +
                    $"nationality layer: {touristNatResult.AddedCount} added, {touristNatResult.UpdatedCount} updated.";
                TempData["ArcGISMessageType"] = "success";
            }
            else
            {
                var errors = string.Join(" | ", new[] { destResult.Error, branchResult.Error, touristsTableResult.Error, touristNatResult.Error }.Where(e => e != null));
                TempData["ArcGISMessage"] = $"❌ ArcGIS push failed: {errors}";
                TempData["ArcGISMessageType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// POST /AdminDashboard/SyncFromArcGIS
        /// Pulls the latest destination data FROM ArcGIS into the local DB.
        /// </summary>
        [HttpPost("SyncFromArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncFromArcGIS()
        {
            var result = await _arcgisSync.SyncDestinationsFromArcGIS();

            if (result.Success)
            {
                TempData["ArcGISMessage"] = $"✅ Pulled from ArcGIS — {result.AddedCount} destinations synced into local DB.";
                TempData["ArcGISMessageType"] = "success";
            }
            else
            {
                TempData["ArcGISMessage"] = $"❌ ArcGIS pull failed: {result.Error}";
                TempData["ArcGISMessageType"] = "danger";
            }

            return RedirectToAction("Index");
        }

        // -----------------------------------------------------------------------
        // Admin -> Add Destination
        // -----------------------------------------------------------------------

        [HttpGet("Destinations/Add")]
        public IActionResult AddDestination()
        {
            var vm = new AddDestinationVM
            {
                Latitude = 0,
                Longitude = 0,
                LocationSelected = false
            };
            return View(vm);
        }

        [HttpPost("Destinations/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDestination(AddDestinationVM vm)
        {
            if (!vm.LocationSelected || (vm.Latitude == 0 && vm.Longitude == 0))
            {
                ModelState.AddModelError(string.Empty, "Please select the Destination location on the map.");
            }

            var isPublic = string.Equals(vm.Category?.Trim(), "Public", StringComparison.OrdinalIgnoreCase);
            if (isPublic)
            {
                // Public destinations are always free-form access: no booking or
                // ticket requirement and an all-day schedule are persisted so the
                // existing ArcGIS fields remain populated.
                vm.TicketRequired = "No";
                vm.EgyptianPrice = null;
                vm.StudentEgyptianPrice = null;
                vm.ForeignPrice = null;
                vm.StudentForeignPrice = null;
                vm.Booking = null;
                vm.SelectedDays = new List<string> { "All Days" };
                vm.OpenAt = 0;
                vm.CloseAt = 23;
            }

            var externalImageUrls = (vm.ExternalImageUrls ?? new List<string>())
                .Select(url => url?.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .Select(url => url!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (vm.ExternalImageUrls?.Any(url =>
                !string.IsNullOrWhiteSpace(url)
                && (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))) == true)
            {
                ModelState.AddModelError(nameof(vm.ExternalImageUrls), "Each external image URL must be a valid absolute URL.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // The layer has an Images URL field but no attachment support. Store
            // uploaded files under wwwroot so the URLs remain accessible to all
            // existing destination consumers after ArcGIS is created.
            var uploadedImageUrls = new List<string>();
            if (vm.ImageFiles != null && vm.ImageFiles.Count > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "destinations");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in vm.ImageFiles)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    if (!allowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError("ImageFiles", $"File '{file.FileName}' has an invalid format. Allowed: JPG, PNG, WEBP.");
                        return View(vm);
                    }
                    if (file.Length > 10 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFiles", $"File '{file.FileName}' exceeds the 10MB size limit.");
                        return View(vm);
                    }

                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    await using var stream = new FileStream(filePath, FileMode.CreateNew);
                    await file.CopyToAsync(stream);
                    uploadedImageUrls.Add($"/uploads/destinations/{fileName}");
                }
            }

            var allImageUrls = uploadedImageUrls
                .Concat(externalImageUrls)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            string? photoUrlsCombined = allImageUrls.Any() ? string.Join("\n", allImageUrls) : null;
            string? daysCombined = vm.SelectedDays.Any() ? string.Join(", ", vm.SelectedDays) : null;

            var destination = new Destination
            {
                Name = vm.Name.Trim(),
                ArabicName = string.IsNullOrWhiteSpace(vm.ArabicName) ? null : vm.ArabicName.Trim(),
                City = vm.City.Trim(),
                Category = vm.Category.Trim(),
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                Tags = string.IsNullOrWhiteSpace(vm.Tags) ? null : vm.Tags.Trim(),
                TicketRequired = vm.TicketRequired,
                EgyptianPrice = vm.EgyptianPrice,
                StudentEgyptianPrice = vm.StudentEgyptianPrice,
                ForeignPrice = vm.ForeignPrice,
                StudentForeignPrice = vm.StudentForeignPrice,
                Days = daysCombined,
                OpenAt = vm.OpenAt,
                CloseAt = vm.CloseAt,
                Booking = vm.Booking,
                PhotoUrls = photoUrlsCombined,
                Location = new NetTopologySuite.Geometries.Point(vm.Longitude, vm.Latitude) { SRID = 4326 },
                Status = "Active",
                Visits = 0,
                Rating = 0m
            };

            // 1. Create Feature in ArcGIS FIRST (Source of Truth)
            var (arcgisSuccess, arcgisError, objectId, createdId) = await _arcgisSync.AddDestinationToArcGISAsync(destination);

            if (!arcgisSuccess)
            {
                ModelState.AddModelError(string.Empty, $"Unable to create the Destination in ArcGIS: {arcgisError}. Please try again.");
                return View(vm);
            }

            // 2. ArcGIS confirmed success -> Sync local DB to ensure local IDs match remote layer
            if (createdId.HasValue)
            {
                destination.Id = createdId.Value;
            }

            // Pull fresh or save to ensure synchronization across website
            await _arcgisSync.SyncDestinationsFromArcGIS();

            TempData["ArcGISMessage"] = $"Destination '{destination.Name}' was successfully created in ArcGIS and synced locally!";
            TempData["ArcGISMessageType"] = "success";

            return RedirectToAction("Index");
        }
    }
}
