using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly INotificationService _notificationService;

        public NotificationBellViewComponent(ISponsorRepository sponsorRepo, INotificationService notificationService)
        {
            _sponsorRepo = sponsorRepo;
            _notificationService = notificationService;
        }

        public IViewComponentResult Invoke()
        {
            if (!User!.Identity!.IsAuthenticated || !User.IsInRole("Sponsor"))
                return View("Default", new NotificationBellVM { UnreadCount = 0, SponsorId = 0 });

            var userId = _sponsorRepo.GetAll()
                .Where(s => s.ApplicationUserId == ((System.Security.Claims.ClaimsPrincipal)User).FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)
                .Select(s => s.Id)
                .FirstOrDefault();

            if (userId == 0)
                return View("Default", new NotificationBellVM { UnreadCount = 0, SponsorId = 0 });

            var unreadCount = _notificationService.GetUnreadCount(userId);
            var vm = new NotificationBellVM { UnreadCount = unreadCount, SponsorId = userId };
            return View("Default", vm);
        }
    }
}
