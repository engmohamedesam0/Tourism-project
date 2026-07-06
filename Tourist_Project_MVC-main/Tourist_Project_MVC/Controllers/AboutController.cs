using Microsoft.AspNetCore.Mvc;

namespace Tourist_Project_MVC.Controllers
{
    public class AboutController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
