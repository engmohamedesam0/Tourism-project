using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class AdminApprovalBellViewComponent : ViewComponent
    {
        private readonly TouristContext _context;
        public AdminApprovalBellViewComponent(TouristContext context) => _context = context;

        public IViewComponentResult Invoke()
        {
            var pending = _context.SponsorApprovalRequests.Count(r => r.Status == "Pending");
            return View("Default", new AdminApprovalBellVM { PendingCount = pending });
        }
    }
}
