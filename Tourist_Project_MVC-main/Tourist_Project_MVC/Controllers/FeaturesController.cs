using Microsoft.AspNetCore.Mvc;

namespace Tourist_Project_MVC.Controllers
{
    // Mobile app marketing / features page. Public, no model required.
    public class FeaturesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
