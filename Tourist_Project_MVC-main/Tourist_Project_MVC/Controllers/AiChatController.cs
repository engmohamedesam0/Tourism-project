using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    public class AiChatController : Controller
    {
        private readonly IAiChatService _aiChatService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITouristRepository _touristRepo;

        public AiChatController(
            IAiChatService aiChatService,
            UserManager<ApplicationUser> userManager,
            ITouristRepository touristRepo)
        {
            _aiChatService = aiChatService;
            _userManager = userManager;
            _touristRepo = touristRepo;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send([FromBody] AiChatRequestVM request, CancellationToken ct)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required." });
            }

            Tourist? tourist = null;

            if (User.Identity?.IsAuthenticated == true && User.IsInRole("User"))
            {
                var appUser = await _userManager.GetUserAsync(User);
                if (appUser != null)
                {
                    tourist = _touristRepo.GetOrCreateByApplicationUser(appUser);
                }
            }

            var result = await _aiChatService.GetReplyAsync(request, tourist, ct);
            return Json(result);
        }
    }
}
