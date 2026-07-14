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

            var userId = ((System.Security.Claims.ClaimsPrincipal)User).FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            string role;
            int unreadCount = 0;
            int sponsorId = 0;

            if (User.IsInRole("Sponsor"))
            {
                role = "Sponsor";
                sponsorId = _sponsorRepo.GetAll()
                    .Where(s => s.ApplicationUserId == userId)
                    .Select(s => s.Id)
                    .FirstOrDefault();

                if (sponsorId != 0)
                    unreadCount = _notificationService.GetUnreadCount(sponsorId);
            }
            else if (User.IsInRole("Admin"))
            {
                role = "Admin";
            }
            else
            {
                role = "Tourist";
            }

            return View("Default", new NotificationBellVM
            {
                UnreadCount = unreadCount,
                SponsorId = sponsorId,
                UserRole = role
            });
        }
    }
}
