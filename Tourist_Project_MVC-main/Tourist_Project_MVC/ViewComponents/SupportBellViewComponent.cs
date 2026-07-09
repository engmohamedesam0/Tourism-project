using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class SupportBellViewComponent : ViewComponent
    {
        private readonly ISponsorRepository _sponsorRepo;
        private readonly ISupportTicketService _supportTicketService;

        public SupportBellViewComponent(ISponsorRepository sponsorRepo, ISupportTicketService supportTicketService)
        {
            _sponsorRepo = sponsorRepo;
            _supportTicketService = supportTicketService;
        }

        public IViewComponentResult Invoke()
        {
            if (!User!.Identity!.IsAuthenticated || !User.IsInRole("Sponsor"))
                return View("Default", new SupportBellVM { OpenCount = 0, SponsorId = 0 });

            var userId = _sponsorRepo.GetAll()
                .Where(s => s.ApplicationUserId == ((System.Security.Claims.ClaimsPrincipal)User).FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)
                .Select(s => s.Id)
                .FirstOrDefault();

            if (userId == 0)
                return View("Default", new SupportBellVM { OpenCount = 0, SponsorId = 0 });

            var openCount = _supportTicketService.GetBySponsorId(userId).Count(t => t.Status != "Resolved");
            var vm = new SupportBellVM { OpenCount = openCount, SponsorId = userId };
            return View("Default", vm);
        }
    }
}
