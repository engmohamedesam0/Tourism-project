using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;

namespace Tourist_Project_MVC.Controllers
{
    // Notification box endpoints for every signed-in role (Admin / Sponsor / Tourist).
    // Each request is scoped to the current user's recipient, so a user can only
    // read / mark / delete their own notifications.
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly INotificationService _notificationService;

        public NotificationController(ISponsorRepository sponsorRepo, INotificationService notificationService)
        {
            _sponsorRepo = sponsorRepo;
            _notificationService = notificationService;
        }

        private (string Role, int? SponsorId, string? UserId) Recipient()
            => _notificationService.ResolveRecipient(User, _sponsorRepo);

        private int Unread(string role, int? sponsorId, string? userId)
            => _notificationService.GetUnreadCount(role, sponsorId, userId);

        // GET: partial notifications list for the bell dropdown box.
        public IActionResult Panel()
        {
            var (role, sponsorId, userId) = Recipient();

            // Auto-generate the sponsor's "reward redeemed / expired / low stock"
            // notifications on open, like the sponsor page does. For admins,
            // generate the pending-approval and open-ticket notifications that
            // the nav badges count, so they appear in the bell box too.
            if (role == "Sponsor" && sponsorId.HasValue)
                _notificationService.ScanAndCreate(sponsorId.Value);
            else if (role == "Admin")
                _notificationService.ScanAndCreateForAdmin();

            var notifications = _notificationService.GetNotifications(role, sponsorId, userId)
                .Take(10)
                .ToList();

            return PartialView("_NotificationPanel", notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkRead(int id)
        {
            var (role, sponsorId, userId) = Recipient();

            var success = _notificationService.MarkAsRead(id, role, sponsorId, userId);
            if (!success) return NotFound();

            return Json(new { success = true, unreadCount = Unread(role, sponsorId, userId) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var (role, sponsorId, userId) = Recipient();

            _notificationService.MarkAllRead(role, sponsorId, userId);

            return Json(new { success = true, unreadCount = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteNotification(int id)
        {
            var (role, sponsorId, userId) = Recipient();

            var success = _notificationService.Delete(id, role, sponsorId, userId);
            if (!success) return NotFound();

            return Json(new { success = true, unreadCount = Unread(role, sponsorId, userId) });
        }
    }
}
