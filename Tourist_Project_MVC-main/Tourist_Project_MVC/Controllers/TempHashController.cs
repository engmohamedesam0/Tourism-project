using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Controllers
{
    public class TempHashController : Controller
    {
        [HttpGet("/generate-hash")]
        public IActionResult GenerateHash(string password)
        {
            var hasher = new PasswordHasher<ApplicationUser>();
            string hash = hasher.HashPassword(null!, password);
            return Content(hash);
        }
    }
}