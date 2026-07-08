using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public SponsorNotificationController(ISponsorRepository sponsorRepo, INotificationService notificationService, ISupportTicketService supportTicketService)
        {
            _sponsorRepo = sponsorRepo;
            _notificationService = notificationService;
            _supportTicketService = supportTicketService;
        }

        private Sponsor? ResolveCurrentSponsor()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)!.Value;
            return _sponsorRepo.GetOrCreateByApplicationUser(userId, email);
        }

        public IActionResult Index()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            _notificationService.ScanAndCreate(sponsor.Id);

            var notifications = _notificationService.GetNotifications(sponsor.Id);
            var tickets = _supportTicketService.GetBySponsorId(sponsor.Id);

            var vm = new SponsorNotificationVM
            {
                Notifications = notifications,
                SupportTickets = tickets
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

        public IActionResult Support()
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            var tickets = _supportTicketService.GetBySponsorId(sponsor.Id);
            var vm = new SupportTicketVM { Tickets = tickets };
            return View("Support", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSupport(SupportTicketVM vm)
        {
            var sponsor = ResolveCurrentSponsor();
            if (sponsor == null) return RedirectToAction("CompleteProfile", "SponsorPortal");

            if (ModelState.IsValid)
            {
                var ticket = new SupportTicket
                {
                    SponsorId = sponsor.Id,
                    Subject = vm.Subject!,
                    Description = vm.Description!
                };

                _supportTicketService.Create(ticket);
                return RedirectToAction("Support");
            }

            vm.Tickets = _supportTicketService.GetBySponsorId(sponsor.Id);
            return View("Support", vm);
        }
    }
}
