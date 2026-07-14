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
            if (!User!.Identity!.IsAuthenticated || !User.IsInRole("Sponsor"))
                return Content(string.Empty);

            var userId = ((System.Security.Claims.ClaimsPrincipal)User).FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var sponsorId = _sponsorRepo.GetAll()
                .Where(s => s.ApplicationUserId == userId)
                .Select(s => s.Id)
                .FirstOrDefault();

            if (sponsorId == 0)
                return Content(string.Empty);

            var unreadCount = _notificationService.GetUnreadCount(sponsorId);
            return View("Default", new NotificationBellVM { UnreadCount = unreadCount, SponsorId = sponsorId });
        }
    }
}
