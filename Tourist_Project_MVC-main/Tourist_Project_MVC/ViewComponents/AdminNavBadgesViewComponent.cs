using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class AdminNavBadgesViewComponent : ViewComponent
    {
        private readonly TouristContext _context;
        public AdminNavBadgesViewComponent(TouristContext context) => _context = context;

        public IViewComponentResult Invoke(string? badge = null)
        {
            var pendingApprovals = _context.SponsorApprovalRequests.Count(r => r.Status == "Pending");
            var unresolvedSupport = _context.SupportTickets.Count(t => t.Status != "Resolved");

            var vm = new AdminNavBadgesVM
            {
                PendingApprovalsCount = pendingApprovals,
                UnresolvedSupportCount = unresolvedSupport
            };

            ViewData["Badge"] = badge;
            return View("Default", vm);
        }
    }
}
