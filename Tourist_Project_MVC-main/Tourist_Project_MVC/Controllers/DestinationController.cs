using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class DestinationController : Controller
    {
        private readonly IDestinationRepository _repo;
        private readonly TouristContext _context;
        private readonly IGamificationService _gamificationService;
        private readonly IFavoriteRepository _favoriteRepo;
        private readonly IArcGISSyncService _arcgisSync;
        private readonly IReviewService _reviewService;

        public DestinationController(IDestinationRepository repo, TouristContext context, IGamificationService gamificationService, IFavoriteRepository favoriteRepo, IArcGISSyncService arcgisSync, IReviewService reviewService)
        {
            _repo = repo;
            _context = context;
            _gamificationService = gamificationService;
            _favoriteRepo = favoriteRepo;
            _arcgisSync = arcgisSync;
            _reviewService = reviewService;
        }

        // GET: /Destination/Index
        public async Task<IActionResult> Index(string? search, string? status, string? category, string? field, string? filter, string sort = "ObjectId", string direction = "asc", int page = 1, int pageSize = 25)
        {
            var snapshot = await _arcgisSync.GetDestinationSnapshotAsync();
            var all = _repo.GetAll().ToList();
            var databaseById = all.ToDictionary(d => d.Id);
            var records = snapshot.Records
                .Where(r => !databaseById.TryGetValue(r.DatabaseId ?? -1, out _))
                .Select(r => new DestinationSmartRow(r, null))
                .Concat(snapshot.Records.Where(r => databaseById.ContainsKey(r.DatabaseId ?? -1)).Select(r => new DestinationSmartRow(r, databaseById[r.DatabaseId!.Value])))
                .ToList();
            if (!snapshot.Success) records = all.Select(d => new DestinationSmartRow(new ArcGISDestinationRecord(null, d.Id, new Dictionary<string, object?>(), d.Location?.Y, d.Location?.X), d)).ToList();
            var normalizedSearch = search?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch)) records = records.Where(r => r.Feature.Attributes.Values.Any(v => v?.ToString()?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) == true) || r.DatabaseRecord?.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (!string.IsNullOrWhiteSpace(status)) records = records.Where(r => string.Equals(Value(r, "Status"), status, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(category)) records = records.Where(r => string.Equals(Value(r, "Category"), category, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(field) && !string.IsNullOrWhiteSpace(filter)) records = records.Where(r => Value(r, field)?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true).ToList();
            records = direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? records.OrderByDescending(r => SortValue(r, sort)).ToList() : records.OrderBy(r => SortValue(r, sort)).ToList();
            // Destinations are intentionally rendered as one continuous admin data view.
            // Keep the legacy parameters for route compatibility, but do not slice the
            // filtered result set into pages.
            page = 1;
            var totalRecords = records.Count;
            pageSize = Math.Max(1, totalRecords);
            var smartModel = new DestinationSmartIndexVM { Fields = snapshot.Fields, Records = records, Search = search, Status = status, Category = category, Field = field, Filter = filter, Sort = sort, Direction = direction, Page = page, PageSize = pageSize, TotalRecords = totalRecords, HasError = !snapshot.Success, Error = snapshot.Error, StatusValues = snapshot.Records.Select(r => AttributeText(r.Attributes, "Status")).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList()!, CategoryValues = snapshot.Records.Select(r => AttributeText(r.Attributes, "Category")).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v).ToList()! };

            ViewBag.AllCount = all.Count();
            ViewBag.ActiveCount = all.Count(d => d.Status == "Active");
            ViewBag.PendingCount = all.Count(d => d.Status == "Pending");
            ViewBag.InactiveCount = all.Count(d => d.Status == "Inactive");

            ViewBag.Categories = all
                .Where(d => d.Category != null)
                .Select(d => d.Category)
                .Distinct()
                .ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Category = category;

            if (User.IsInRole("User"))
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
                if (tourist != null)
                {
                    ViewBag.FavoritedDestinationIds = _favoriteRepo.GetFavoritedItemIds(tourist.Id, FavoriteItemType.Destination);
                }
            }

            // Top stat-box row (real aggregates, query-level).
            var totalVisits = _context.Destinations.Sum(d => (int?)d.Visits) ?? 0;
            var topCategory = _context.Destinations
                .Where(d => d.Category != null)
                .GroupBy(d => d.Category)
                .Select(g => new { Cat = g.Key, Visits = g.Sum(d => d.Visits) })
                .OrderByDescending(g => g.Visits)
                .Select(g => g.Cat)
                .FirstOrDefault() ?? "—";

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-geo-alt-fill", Color = "blue", Value = all.Count().ToString("N0"), Label = "Total Destinations" },
                new StatBoxItem { IconClass = "bi-eye-fill", Color = "green", Value = totalVisits.ToString("N0"), Label = "Total Visits" },
                new StatBoxItem { IconClass = "bi-check-circle-fill", Color = "gold", Value = all.Count(d => d.Status == "Active").ToString("N0"), Label = "Active Destinations" },
                new StatBoxItem { IconClass = "bi-bar-chart-fill", Color = "purple", Value = topCategory, Label = "Top Category (by Visits)" }
            };

            return View("Index", smartModel);
        }

        private static string? AttributeText(IReadOnlyDictionary<string, object?> attributes, string field) => attributes.TryGetValue(field, out var value) ? value?.ToString() : null;

        private static string? Value(DestinationSmartRow row, string field)
        {
            var text = AttributeText(row.Feature.Attributes, field);
            if (text != null) return text;
            return field switch { "English_Name" => row.DatabaseRecord?.Name, "Arabic_Name" => row.DatabaseRecord?.ArabicName, "Governorate" => row.DatabaseRecord?.City, "Category" => row.DatabaseRecord?.Category, "Status" => row.DatabaseRecord?.Status, _ => null };
        }

        private static object SortValue(DestinationSmartRow row, string field) => Value(row, field) ?? (object)(row.Feature.ObjectId ?? row.DatabaseRecord?.Id ?? int.MaxValue);

        // GET: /Destination/Details
        public async Task<IActionResult> Details(int id)
        {
            var snapshot = await _arcgisSync.GetDestinationSnapshotAsync(id);
            var feature = snapshot.Records.FirstOrDefault();
            var destination = _repo.GetById(id);
            if (feature == null && destination == null) return NotFound();

            if (destination != null)
            {
                destination.Visits++;
                _repo.Update(destination);
                _repo.Save();
            }

            // Context-aware back target: respect the referrer so tourists return
            // to Explore (filters/scroll intact) and admins return to the admin list.
            var referrer = Request.Headers["Referer"].ToString();
            string backUrl = "/Destination";
            if (!string.IsNullOrEmpty(referrer))
            {
                if (referrer.Contains("/Explore", System.StringComparison.OrdinalIgnoreCase))
                    backUrl = "/Explore";
                else if (referrer.Contains("/Trip", System.StringComparison.OrdinalIgnoreCase))
                    backUrl = "/Trip";
            }
            ViewBag.BackUrl = backUrl;

            // Render the redesigned record page when the destination exists in the
            // database (hero image slideshow, Basic/Visiting/Location/GIS cards...).
            // SmartDetails remains the fallback for ArcGIS-only features.
            if (destination != null)
            {
                ViewBag.ReviewSection = _reviewService.GetSection(
                    "Destination", destination.Id, destination.Name,
                    canAdd: User.IsInRole("User"));
                return View("Details", destination);
            }

            return View("SmartDetails", new DestinationSmartDetailsVM
            {
                Fields = snapshot.Fields,
                Feature = feature,
                DatabaseRecord = destination,
                Error = snapshot.Success ? null : snapshot.Error
            });
        }

        // GET: /Destination/Create
        public IActionResult Create()
        {
            return View("ReadOnlyNotice");
        }

        // POST: /Destination/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Destination destination, [Range(-90, 90)] double Lat, [Range(-180, 180)] double Long)
        {
            TempData["DestinationMessage"] = "Destinations are managed via ArcGIS. Local CRUD is disabled.";
            TempData["DestinationMessageType"] = "warning";
            return RedirectToAction("Index");
        }

        // GET: /Destination/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var snapshot = await _arcgisSync.GetDestinationSnapshotAsync(id);
            var feature = snapshot.Records.FirstOrDefault();
            var destination = _repo.GetById(id);
            if (feature == null || destination == null) return NotFound();

            return View("SmartEdit", new DestinationSmartEditVM
            {
                DatabaseId = id,
                ObjectId = feature.ObjectId,
                Fields = snapshot.Fields,
                Values = snapshot.Fields.ToDictionary(f => f.Name, f => feature.Attributes.TryGetValue(f.Name, out var value) ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : null, StringComparer.OrdinalIgnoreCase),
                Error = snapshot.Success ? null : snapshot.Error
            });
        }

        // POST: /Destination/Edit
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DestinationSmartEditVM model)
        {
            var existing = _repo.GetById(model.DatabaseId);
            if (existing == null) return NotFound();
            var snapshot = await _arcgisSync.GetDestinationSnapshotAsync(model.DatabaseId);
            var feature = snapshot.Records.FirstOrDefault();
            if (feature == null) return NotFound();

            var values = model.Values ?? new(StringComparer.OrdinalIgnoreCase);
            string Text(string field) => values.TryGetValue(field, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
            bool TryInt(string field, out int? value)
            {
                value = null;
                var text = Text(field);
                if (string.IsNullOrWhiteSpace(text)) return true;
                if (!int.TryParse(text, out var parsed)) return false;
                value = parsed;
                return true;
            }
            bool TryDecimal(string field, out decimal? value) { value = null; return string.IsNullOrWhiteSpace(Text(field)) || decimal.TryParse(Text(field), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && (value = parsed).HasValue; }
            bool TryDouble(string field, out double value) => double.TryParse(Text(field), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);

            if (string.IsNullOrWhiteSpace(Text("English_Name"))) ModelState.AddModelError("Values[English_Name]", "English name is required.");
            if (!TryInt("Visits", out var visits)) ModelState.AddModelError("Values[Visits]", "Visits must be a whole number.");
            if (!TryInt("ForeignPrice", out var foreignPrice)) ModelState.AddModelError("Values[ForeignPrice]", "Foreign price must be a whole number.");
            if (!TryInt("StudentForeignPrice", out var studentForeignPrice)) ModelState.AddModelError("Values[StudentForeignPrice]", "Student foreign price must be a whole number.");
            if (!TryInt("EgyptianPrice", out var egyptianPrice)) ModelState.AddModelError("Values[EgyptianPrice]", "Egyptian price must be a whole number.");
            if (!TryInt("StudentEgyptianPrice", out var studentEgyptianPrice)) ModelState.AddModelError("Values[StudentEgyptianPrice]", "Student Egyptian price must be a whole number.");
            if (!TryInt("Open_at", out var openAt)) ModelState.AddModelError("Values[Open_at]", "Opening hour must be a whole number.");
            if (!TryInt("Close_at", out var closeAt)) ModelState.AddModelError("Values[Close_at]", "Closing hour must be a whole number.");
            if (!TryInt("Rating", out var ratingInt)) ModelState.AddModelError("Values[Rating]", "Rating must be a whole number.");
            if (!TryDouble("Latitiude", out var latitude) || latitude is < -90 or > 90) ModelState.AddModelError("Values[Latitiude]", "Latitude must be between -90 and 90.");
            if (!TryDouble("Longitude", out var longitude) || longitude is < -180 or > 180) ModelState.AddModelError("Values[Longitude]", "Longitude must be between -180 and 180.");

            if (!ModelState.IsValid)
            {
                return View("SmartEdit", new DestinationSmartEditVM { DatabaseId = model.DatabaseId, ObjectId = feature.ObjectId, Fields = snapshot.Fields, Values = values, Error = "Review the highlighted values before saving." });
            }

            // Build a candidate without mutating the tracked database entity. ArcGIS is first.
            var candidate = new Destination
            {
                Id = existing.Id,
                Name = Text("English_Name"),
                ArabicName = NullIfEmpty(Text("Arabic_Name")),
                City = Text("Governorate"),
                Category = NullIfEmpty(Text("Category")),
                Description = NullIfEmpty(Text("Description")),
                Status = string.IsNullOrWhiteSpace(Text("Status")) ? "Active" : Text("Status"),
                Visits = visits ?? 0,
                Rating = ratingInt,
                Tags = NullIfEmpty(Text("Tags")),
                PhotoUrls = NullIfEmpty(Text("Images"))?.Replace("|", "\n"),
                TicketRequired = NullIfEmpty(Text("TicketRequired")),
                ForeignPrice = foreignPrice,
                StudentForeignPrice = studentForeignPrice,
                EgyptianPrice = egyptianPrice,
                StudentEgyptianPrice = studentEgyptianPrice,
                Days = NullIfEmpty(Text("Days")),
                OpenAt = openAt,
                CloseAt = closeAt,
                Booking = NullIfEmpty(Text("Booking")),
                Location = new Point(longitude, latitude) { SRID = 4326 }
            };

            var syncResult = await _arcgisSync.UpdateDestinationOnArcGISAsync(candidate);
            if (!syncResult.Success)
            {
                TempData["DestinationMessage"] = $"ArcGIS update failed. The database was not changed: {syncResult.Error}";
                TempData["DestinationMessageType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                existing.Name = candidate.Name; existing.ArabicName = candidate.ArabicName; existing.City = candidate.City; existing.Category = candidate.Category;
                existing.Description = candidate.Description; existing.Status = candidate.Status; existing.Visits = candidate.Visits; existing.Rating = candidate.Rating;
                existing.Tags = candidate.Tags; existing.PhotoUrls = candidate.PhotoUrls; existing.TicketRequired = candidate.TicketRequired; existing.ForeignPrice = candidate.ForeignPrice;
                existing.StudentForeignPrice = candidate.StudentForeignPrice; existing.EgyptianPrice = candidate.EgyptianPrice; existing.StudentEgyptianPrice = candidate.StudentEgyptianPrice;
                existing.Days = candidate.Days; existing.OpenAt = candidate.OpenAt; existing.CloseAt = candidate.CloseAt; existing.Booking = candidate.Booking; existing.Location = candidate.Location;
                _repo.Update(existing);
                _repo.Save();
            }
            catch (Exception ex)
            {
                TempData["DestinationMessage"] = $"ArcGIS was updated, but database synchronization failed. Contact an administrator. ({ex.Message})";
                TempData["DestinationMessageType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            TempData["DestinationMessage"] = $"Destination '{candidate.Name}' was updated in ArcGIS and synchronized to the database.";
            TempData["DestinationMessageType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // GET: /Destination/Delete/5
        [Authorize(Roles = "Admin")]
        public IActionResult Delete(int id)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();
            return View("DeleteSmart", destination);
        }

        // POST: /Destination/Delete
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();

            // ArcGIS is authoritative: do not mutate any local rows until the
            // exact remote feature has been deleted and confirmed.
            var arcgisResult = await _arcgisSync.DeleteDestinationFromArcGISAsync(id);
            if (!arcgisResult.Success)
            {
                TempData["DestinationMessage"] = $"The destination was not deleted because ArcGIS could not confirm the removal: {arcgisResult.Error}";
                TempData["DestinationMessageType"] = "danger";
                return RedirectToAction(nameof(Index));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var missions = _context.Missions.Where(m => m.DestinationId == id).ToList();
                var missionIds = missions.Select(m => m.Id).ToList();
                if (missionIds.Count > 0)
                {
                    _context.UserMissions.RemoveRange(_context.UserMissions.Where(um => missionIds.Contains(um.MissionId)));
                    _context.Missions.RemoveRange(missions);
                }
                _context.TripDestinations.RemoveRange(_context.TripDestinations.Where(td => td.DestinationId == id));
                _context.SiteReviews.Where(r => r.DestinationId == id).ToList().ForEach(r => r.DestinationId = null);
                _context.Favorites.RemoveRange(_context.Favorites.Where(f => f.ItemType == FavoriteItemType.Destination && f.ItemId == id));
                _repo.Delete(id);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["DestinationMessage"] = $"ArcGIS deletion succeeded, but database synchronization failed. The local record requires reconciliation. ({ex.Message})";
                TempData["DestinationMessageType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            TempData["DestinationMessage"] = $"Destination '{destination.Name}' was deleted from ArcGIS and the database.";
            TempData["DestinationMessageType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int id, [Bind("Rating,Comment")] SiteReview vm)
        {
            var destination = _repo.GetById(id);
            if (destination == null) return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tourist = userId != null
                ? _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId)
                : null;
            if (tourist == null)
            {
                TempData["DestinationMessage"] = "Please sign in as a tourist to review this destination.";
                TempData["DestinationMessageType"] = "warning";
                return RedirectToAction("Details", new { id });
            }

            if (!ModelState.IsValid || vm.Rating < 1 || vm.Rating > 5)
            {
                TempData["DestinationMessage"] = "Please choose a rating between 1 and 5 stars.";
                TempData["DestinationMessageType"] = "danger";
                return RedirectToAction("Details", new { id });
            }

            if (string.IsNullOrWhiteSpace(vm.Comment))
            {
                TempData["DestinationMessage"] = "Please write a short review before submitting.";
                TempData["DestinationMessageType"] = "danger";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                var review = new SiteReview
                {
                    Rating = vm.Rating,
                    Comment = vm.Comment.Trim(),
                    DestinationId = id,
                    TouristId = tourist.Id,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };

                _context.SiteReviews.Add(review);
                _context.SaveChanges();

                // Destination.Rating must always reflect the tourists' reviews.
                _reviewService.SyncDestinationRating(id);
                _ = _gamificationService.AwardXPAsync(tourist.Id, 25, "review");

                TempData["DestinationMessage"] = "Thanks! Your review has been published.";
                TempData["DestinationMessageType"] = "success";
            }
            catch
            {
                TempData["DestinationMessage"] = "Something went wrong while saving your review. Please try again.";
                TempData["DestinationMessageType"] = "danger";
            }

            return RedirectToAction("Details", new { id });
        }
    }
}