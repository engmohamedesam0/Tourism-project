using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Sponsor")]
    public class SponsorNotificationController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly INotificationService _notificationService;
        private readonly ISupportTicketService _supportTicketService;
        private readonly IWebHostEnvironment _env;
        private readonly TouristContext _context;

        public SponsorNotificationController(
            ISponsorRepository sponsorRepo,
            INotificationService notificationService,
            ISupportTicketService supportTicketService,
            IWebHostEnvironment env,
            TouristContext context)
        {
            _sponsorRepo = sponsorRepo;
            _notificationService = notificationService;
            _supportTicketService = supportTicketService;
            _env = env;
            _context = context;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        // GET: Notifications page (full). Supports All / Read / Unread filter.
        public IActionResult Index(string? filter)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            _notificationService.ScanAndCreate(sponsor.Id);

            bool? isRead = filter?.ToLower() switch
            {
                "read" => true,
                "unread" => false,
                _ => null
            };

            var notifications = _notificationService.GetNotifications(sponsor.Id, isRead);

            var vm = new SponsorNotificationVM
            {
                Notifications = notifications,
                SupportTickets = new(),
                Filter = filter
            };

            return View("Index", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkRead(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var success = _notificationService.MarkAsRead(id, sponsor.Id);
            if (!success) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, unreadCount = _notificationService.GetUnreadCount(sponsor.Id) });

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            _notificationService.MarkAllRead(sponsor.Id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, unreadCount = 0 });

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteNotification(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var success = _notificationService.Delete(id, sponsor.Id);
            if (!success) return NotFound();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, unreadCount = _notificationService.GetUnreadCount(sponsor.Id) });

            return RedirectToAction("Index");
        }

        // GET: partial notifications list for the nav dropdown.
        public IActionResult Panel()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            _notificationService.ScanAndCreate(sponsor.Id);

            var notifications = _notificationService.GetNotifications(sponsor.Id)
                .OrderByDescending(n => n.CreatedDate)
                .Take(10)
                .ToList();

            return PartialView("_NotificationPanel", notifications);
        }

        // GET: Support tickets page.
        public IActionResult Support()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var tickets = _supportTicketService.GetBySponsorId(sponsor.Id);

            // Top stat-box row (scoped to this sponsor, query-level aggregates).
            var sponsorId = sponsor.Id;
            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-ticket-detailed-fill", Color = "blue", Value = _context.SupportTickets.Count(t => t.SponsorId == sponsorId).ToString("N0"), Label = "Tickets Submitted" },
                new StatBoxItem { IconClass = "bi-envelope-open-fill", Color = "amber", Value = _context.SupportTickets.Count(t => t.SponsorId == sponsorId && t.Status == "Open").ToString("N0"), Label = "Open Tickets" },
                new StatBoxItem { IconClass = "bi-check2-circle", Color = "green", Value = _context.SupportTickets.Count(t => t.SponsorId == sponsorId && t.Status == "Resolved").ToString("N0"), Label = "Resolved Tickets" }
            };

            var vm = new SupportTicketVM
            {
                Tickets = tickets,
                Category = null
            };
            return View("Support", vm);
        }

        // GET: single ticket detail (shows category, attachment, admin response).
        public IActionResult SupportDetails(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var ticket = _supportTicketService.GetById(id, sponsor.Id);
            if (ticket == null) return NotFound();

            return View("SupportDetails", ticket);
        }

        // GET: partial ticket list for the Support nav dropdown.
        public IActionResult SupportPanel()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var tickets = _supportTicketService.GetBySponsorId(sponsor.Id)
                .Take(5)
                .ToList();

            return PartialView("_SupportPanel", tickets);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSupport(SupportTicketVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            if (string.IsNullOrEmpty(vm.Category) || !SupportTicketVM.Categories.Contains(vm.Category))
                ModelState.AddModelError("Category", "Please choose a valid category.");

            var attachmentPath = SaveAttachment(vm.Attachment);

            if (ModelState.IsValid)
            {
                var ticket = new SupportTicket
                {
                    SponsorId = sponsor.Id,
                    Subject = vm.Subject!,
                    Description = vm.Description!,
                    Category = vm.Category!,
                    AttachmentPath = attachmentPath
                };

                _supportTicketService.Create(ticket);
                return RedirectToAction("Support");
            }

            vm.Tickets = _supportTicketService.GetBySponsorId(sponsor.Id);
            return View("Support", vm);
        }

        // GET: Tourist tickets routed to the signed-in sponsor.
        public IActionResult TouristTickets()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var tickets = _supportTicketService.GetTouristTicketsForSponsor(sponsor.Id);

            var vm = new SupportTicketVM
            {
                Tickets = tickets,
                Category = null
            };
            return View("TouristTickets", vm);
        }

        // GET: single tourist ticket detail (for sponsor).
        public IActionResult TouristTicketDetails(int id)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var ticket = _supportTicketService.GetTouristTicketForSponsor(id, sponsor.Id);
            if (ticket == null) return NotFound();

            return View("TouristTicketDetails", ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RespondToTouristTicket(int id, string sponsorResponse)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var ticket = _supportTicketService.GetTouristTicketForSponsor(id, sponsor.Id);
            if (ticket == null) return NotFound();

            if (string.IsNullOrWhiteSpace(sponsorResponse))
            {
                ModelState.AddModelError("sponsorResponse", "A response message is required.");
                return RedirectToAction("TouristTicketDetails", new { id });
            }

            ticket.SponsorResponse = sponsorResponse.Trim();
            ticket.SponsorRespondedDate = DateTime.Now;

            if (ticket.Status == "Open")
                ticket.Status = "In Progress";

            _supportTicketService.Update(ticket);

            return RedirectToAction("TouristTicketDetails", new { id });
        }

        private string? SaveAttachment(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp",   // images
                ".mp4", ".webm", ".mov", ".mkv", ".avi"      // videos
            };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError("Attachment", "Only image or video files are allowed.");
                return null;
            }
            if (file.Length > 15 * 1024 * 1024)
            {
                ModelState.AddModelError("Attachment", "Attachment must be 15 MB or smaller.");
                return null;
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "support-attachments");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            return $"/uploads/support-attachments/{fileName}";
        }
    }
}
