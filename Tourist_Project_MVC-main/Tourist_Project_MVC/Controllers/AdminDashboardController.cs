using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
        private readonly ISyncStateManager _syncStateManager;
        private readonly IConfiguration _config;
        private readonly ILogger<AdminDashboardController> _logger;

        public AdminDashboardController(
            TouristContext context,
            IArcGISSyncService arcgisSync,
            ISyncStateManager syncStateManager,
            IConfiguration config,
            ILogger<AdminDashboardController> logger)
        {
            _context = context;
            _arcgisSync = arcgisSync;
            _syncStateManager = syncStateManager;
            _config = config;
            _logger = logger;
        }

        // -----------------------------------------------------------------------
        // Admin Dashboard -> ArcGIS Online Dashboard (Experience Builder)
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

        [HttpGet("SyncStatus")]
        public IActionResult GetSyncStatus()
        {
            return Json(_syncStateManager.GetStatus());
        }

        // -----------------------------------------------------------------------
        // ArcGIS On-Demand Sync Actions
        // -----------------------------------------------------------------------

        /// <summary>
        /// POST /AdminDashboard/SyncToArcGIS
        /// Pushes changes from Website Database -> ArcGIS Feature Layer.
        /// </summary>
        [HttpPost("SyncToArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncToArcGIS()
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                         || Request.Headers["Accept"].ToString().Contains("application/json");

            if (!_syncStateManager.TryBeginSync(out var currentStatus))
            {
                var busyMsg = $"A synchronization operation is already in progress ({currentStatus}). Please wait.";
                if (isAjax)
                    return StatusCode(StatusCodes.Status409Conflict, new { success = false, message = busyMsg, state = currentStatus.ToString() });

                TempData["ArcGISMessage"] = $"⚠️ {busyMsg}";
                TempData["ArcGISMessageType"] = "warning";
                return RedirectToAction("Index");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("=== [SYNC START] Website Database -> ArcGIS Feature Layer ===");

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
                var redemptionsResult = await _arcgisSync.SyncRedemptionsAsync();

                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;

                var allSuccess = destResult.Success && branchResult.Success && touristsTableResult.Success && touristNatResult.Success && redemptionsResult.Success;
                var totalAdded = destResult.AddedCount + branchResult.AddedCount + touristsTableResult.AddedCount + touristNatResult.AddedCount + redemptionsResult.AddedCount;
                var totalUpdated = destResult.UpdatedCount + branchResult.UpdatedCount + touristsTableResult.UpdatedCount + touristNatResult.UpdatedCount + redemptionsResult.UpdatedCount;
                var totalDeleted = destResult.DeletedCount + branchResult.DeletedCount + touristsTableResult.DeletedCount + touristNatResult.DeletedCount + redemptionsResult.DeletedCount;
                var totalFailed = destResult.FailedCount + branchResult.FailedCount + touristsTableResult.FailedCount + touristNatResult.FailedCount + redemptionsResult.FailedCount;

                var errors = new[] { destResult.Error, branchResult.Error, touristsTableResult.Error, touristNatResult.Error, redemptionsResult.Error }
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToList();
                var errorSummary = errors.Any() ? string.Join(" | ", errors) : null;

                var overallResult = allSuccess
                    ? ArcGISSyncResult.Ok(totalAdded, totalUpdated, totalDeleted, totalFailed, duration)
                    : ArcGISSyncResult.Failed(errorSummary ?? "One or more layers failed to sync.", totalAdded, totalUpdated, totalDeleted, totalFailed > 0 ? totalFailed : 1, duration);

                _syncStateManager.EndOperation(overallResult, isSync: true);

                _logger.LogInformation("=== [SYNC END] Added: {Added}, Updated: {Updated}, Deleted: {Deleted}, Failed: {Failed}, Duration: {Duration:F2}s ===",
                    totalAdded, totalUpdated, totalDeleted, totalFailed, duration);

                if (isAjax)
                {
                    return Json(new
                    {
                        success = allSuccess,
                        operation = "SYNC",
                        added = totalAdded,
                        updated = totalUpdated,
                        deleted = totalDeleted,
                        failed = totalFailed,
                        durationSeconds = Math.Round(duration, 2),
                        formattedDuration = $"{duration:F1}s",
                        message = allSuccess ? "✓ Sync completed" : "✕ Sync failed",
                        error = errorSummary,
                        details = new
                        {
                            destinations = destResult,
                            branches = branchResult,
                            tourists = touristsTableResult,
                            nationalities = touristNatResult,
                            redemptions = redemptionsResult
                        }
                    });
                }

                if (allSuccess)
                {
                    TempData["ArcGISMessage"] = $"✅ Sync completed in {duration:F1}s — Added: {totalAdded}, Updated: {totalUpdated}, Deleted: {totalDeleted}";
                    TempData["ArcGISMessageType"] = "success";
                }
                else
                {
                    TempData["ArcGISMessage"] = $"❌ Sync failed: {errorSummary}";
                    TempData["ArcGISMessageType"] = "danger";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;
                _logger.LogError(ex, "=== [SYNC FAILED] Unhandled exception during SYNC ===");

                var failResult = ArcGISSyncResult.Failed(ex.Message, 0, 0, 0, 1, duration);
                _syncStateManager.EndOperation(failResult, isSync: true);

                if (isAjax)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        operation = "SYNC",
                        added = 0,
                        updated = 0,
                        deleted = 0,
                        failed = 1,
                        durationSeconds = Math.Round(duration, 2),
                        formattedDuration = $"{duration:F1}s",
                        message = "✕ Sync failed",
                        error = ex.Message
                    });
                }

                TempData["ArcGISMessage"] = $"❌ Sync failed: {ex.Message}";
                TempData["ArcGISMessageType"] = "danger";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST /AdminDashboard/SyncFromArcGIS
        /// Pulls changes from ArcGIS Feature Layer -> Website Database (Upsert only).
        /// </summary>
        [HttpPost("SyncFromArcGIS")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncFromArcGIS()
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                         || Request.Headers["Accept"].ToString().Contains("application/json");

            if (!_syncStateManager.TryBeginPull(out var currentStatus))
            {
                var busyMsg = $"A synchronization operation is already in progress ({currentStatus}). Please wait.";
                if (isAjax)
                    return StatusCode(StatusCodes.Status409Conflict, new { success = false, message = busyMsg, state = currentStatus.ToString() });

                TempData["ArcGISMessage"] = $"⚠️ {busyMsg}";
                TempData["ArcGISMessageType"] = "warning";
                return RedirectToAction("Index");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("=== [PULL START] ArcGIS Feature Layer -> Website Database ===");

                var destResult = await _arcgisSync.SyncDestinationsFromArcGIS();
                var branchResult = await _arcgisSync.SyncBranchesFromArcGIS();

                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;

                var allSuccess = destResult.Success && branchResult.Success;
                var totalAdded = destResult.AddedCount + branchResult.AddedCount;
                var totalUpdated = destResult.UpdatedCount + branchResult.UpdatedCount;
                var totalDeleted = destResult.DeletedCount + branchResult.DeletedCount;
                var totalFailed = destResult.FailedCount + branchResult.FailedCount;

                var errors = new[] { destResult.Error, branchResult.Error }.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                var errorSummary = errors.Any() ? string.Join(" | ", errors) : null;

                var overallResult = allSuccess
                    ? ArcGISSyncResult.Ok(totalAdded, totalUpdated, totalDeleted, totalFailed, duration)
                    : ArcGISSyncResult.Failed(errorSummary ?? "Pull operation failed.", totalAdded, totalUpdated, totalDeleted, totalFailed > 0 ? totalFailed : 1, duration);

                _syncStateManager.EndOperation(overallResult, isSync: false);

                _logger.LogInformation("=== [PULL END] Added: {Added}, Updated: {Updated}, Deleted: {Deleted}, Failed: {Failed}, Duration: {Duration:F2}s ===",
                    totalAdded, totalUpdated, totalDeleted, totalFailed, duration);

                if (isAjax)
                {
                    return Json(new
                    {
                        success = allSuccess,
                        operation = "PULL",
                        added = totalAdded,
                        updated = totalUpdated,
                        deleted = totalDeleted,
                        failed = totalFailed,
                        durationSeconds = Math.Round(duration, 2),
                        formattedDuration = $"{duration:F1}s",
                        message = allSuccess ? "✓ Pull completed" : "✕ Pull failed",
                        error = errorSummary,
                        details = new
                        {
                            destinations = destResult,
                            branches = branchResult
                        }
                    });
                }

                if (allSuccess)
                {
                    TempData["ArcGISMessage"] = $"✅ Pull completed in {duration:F1}s — Added: {totalAdded}, Updated: {totalUpdated}";
                    TempData["ArcGISMessageType"] = "success";
                }
                else
                {
                    TempData["ArcGISMessage"] = $"❌ Pull failed: {errorSummary}";
                    TempData["ArcGISMessageType"] = "danger";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;
                _logger.LogError(ex, "=== [PULL FAILED] Unhandled exception during PULL ===");

                var failResult = ArcGISSyncResult.Failed(ex.Message, 0, 0, 0, 1, duration);
                _syncStateManager.EndOperation(failResult, isSync: false);

                if (isAjax)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        operation = "PULL",
                        added = 0,
                        updated = 0,
                        deleted = 0,
                        failed = 1,
                        durationSeconds = Math.Round(duration, 2),
                        formattedDuration = $"{duration:F1}s",
                        message = "✕ Pull failed",
                        error = ex.Message
                    });
                }

                TempData["ArcGISMessage"] = $"❌ Pull failed: {ex.Message}";
                TempData["ArcGISMessageType"] = "danger";
                return RedirectToAction("Index");
            }
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

            // 2. ArcGIS confirmed success -> Set stable ID
            if (createdId.HasValue)
            {
                destination.Id = createdId.Value;
            }

            // 3. Upsert destination into local database
            var existing = await _context.Destinations.FirstOrDefaultAsync(d => d.Id == destination.Id);
            if (existing != null)
            {
                existing.Name = destination.Name;
                existing.ArabicName = destination.ArabicName;
                existing.City = destination.City;
                existing.Category = destination.Category;
                existing.Description = destination.Description;
                existing.Tags = destination.Tags;
                existing.PhotoUrls = destination.PhotoUrls;
                existing.Location = destination.Location;
                _context.Destinations.Update(existing);
            }
            else
            {
                _context.Destinations.Add(destination);
            }
            await _context.SaveChangesAsync();

            TempData["ArcGISMessage"] = $"Destination '{destination.Name}' was successfully created in ArcGIS and synchronized locally!";
            TempData["ArcGISMessageType"] = "success";

            return RedirectToAction("Index");
        }
    }
}
