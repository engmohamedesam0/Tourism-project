using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class NotificationFabViewComponent : ViewComponent
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly INotificationService _notificationService;

        public NotificationFabViewComponent(ISponsorRepository sponsorRepo, INotificationService notificationService)
        {
            _sponsorRepo = sponsorRepo;
            _notificationService = notificationService;
        }

        public IViewComponentResult Invoke()
        {
            if (!User!.Identity!.IsAuthenticated)
                return Content(string.Empty);

            var (role, sponsorId, userId) = _notificationService.ResolveRecipient(
                (System.Security.Claims.ClaimsPrincipal)User, _sponsorRepo);

            // For admins, materialize the pending-approval / open-ticket bell
            // notifications up front so the badge matches the nav counts.
            if (role == "Admin")
                _notificationService.ScanAndCreateForAdmin();

            int unreadCount = _notificationService.GetUnreadCount(role, sponsorId, userId);

            return View("Default", new NotificationBellVM
            {
                UnreadCount = unreadCount,
                SponsorId = sponsorId ?? 0,
                UserId = userId,
                UserRole = role
            });
        }
    }
}
