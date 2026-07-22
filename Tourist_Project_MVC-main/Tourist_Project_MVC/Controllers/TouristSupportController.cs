using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User")]
    public class TouristSupportController : Controller
    {
        private readonly ITouristRepository _touristRepo;
        private readonly ISupportTicketService _supportTicketService;
        private readonly INotificationService _notificationService;
        private readonly ISponsorRepository _sponsorRepo;
        private readonly IWebHostEnvironment _env;
        private readonly TouristContext _context;

        public TouristSupportController(
            ITouristRepository touristRepo,
            ISupportTicketService supportTicketService,
            INotificationService notificationService,
            ISponsorRepository sponsorRepo,
            IWebHostEnvironment env,
            TouristContext context)
        {
            _touristRepo = touristRepo;
            _supportTicketService = supportTicketService;
            _notificationService = notificationService;
            _sponsorRepo = sponsorRepo;
            _env = env;
            _context = context;
        }

        private Tourist ResolveCurrentTourist()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var appUser = _context.Users.FirstOrDefault(u => u.Id == userId);
            return _touristRepo.GetOrCreateByApplicationUser(appUser);
        }

        // GET: Support tickets page for the signed-in tourist.
        public IActionResult Index()
        {
            var tourist = ResolveCurrentTourist();
            var tickets = _supportTicketService.GetByTouristId(tourist.Id);
            var interactedSponsors = _sponsorRepo.GetInteractedSponsors(tourist.Id).ToList();
            var vm = new SupportTicketVM
            {
                Tickets = tickets,
                Category = null,
                RecipientType = "Admin",
                AvailableSponsors = new SelectList(interactedSponsors, "Id", "Name")
            };
            ViewBag.HasInteractedSponsors = interactedSponsors.Any();
            return View("Index", vm);
        }

        // GET: single ticket detail.
        public IActionResult Details(int id)
        {
            var tourist = ResolveCurrentTourist();
            var ticket = _supportTicketService.GetByIdForTourist(id, tourist.Id);
            if (ticket == null) return NotFound();

            return View("Details", ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSupport(SupportTicketVM vm)
        {
            var tourist = ResolveCurrentTourist();
            var interactedSponsors = _sponsorRepo.GetInteractedSponsors(tourist.Id).ToList();
            var interactedSponsorIds = interactedSponsors.Select(s => s.Id).ToHashSet();

            if (string.IsNullOrEmpty(vm.Category) || !SupportTicketVM.Categories.Contains(vm.Category))
                ModelState.AddModelError("Category", "Please choose a valid category.");

            if (vm.RecipientType == "Sponsor")
            {
                if (!interactedSponsorIds.Any())
                {
                    ModelState.AddModelError("RecipientType", "You can only contact a sponsor you've redeemed a reward from or reviewed.");
                }
                else if (!vm.SponsorId.HasValue || vm.SponsorId.Value <= 0)
                {
                    ModelState.AddModelError("SponsorId", "Please choose a valid sponsor.");
                }
                else if (!interactedSponsorIds.Contains(vm.SponsorId.Value))
                {
                    ModelState.AddModelError("SponsorId", "You can only contact a sponsor you've interacted with.");
                }
            }

            var attachmentPath = SaveAttachment(vm.Attachment);

            if (ModelState.IsValid)
            {
                var ticket = new SupportTicket
                {
                    TouristId = tourist.Id,
                    Subject = vm.Subject!,
                    Description = vm.Description!,
                    Category = vm.Category!,
                    AttachmentPath = attachmentPath,
                    SponsorId = vm.RecipientType == "Sponsor" ? vm.SponsorId : null
                };

                if (vm.RecipientType == "Sponsor" && vm.SponsorId.HasValue)
                {
                    _notificationService.Create(
                        vm.SponsorId.Value,
                        "TouristTicketRouted",
                        $"A tourist routed a support ticket to you: \"{ticket.Subject}\".",
                        "TouristSupportTicket",
                        ticket.Id);
                }

                _supportTicketService.Create(ticket);
                return RedirectToAction(nameof(Index));
            }

            vm.Tickets = _supportTicketService.GetByTouristId(tourist.Id);
            vm.AvailableSponsors = new SelectList(interactedSponsors, "Id", "Name");
            ViewBag.HasInteractedSponsors = interactedSponsors.Any();
            return View("Index", vm);
        }

        private string? SaveAttachment(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[]
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                ".mp4", ".webm", ".mov", ".mkv", ".avi"
            };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError("Attachment", "Only image or video files are allowed.");
                return null;
            }
            if (file.Length > 15 * 1024 * 1024)
            {
                ModelState.AddModelError("Attachment", "Attachment must be 15 MB or smaller.");
                return null;
            }

            var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads", "support-attachments");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = System.IO.Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            return $"/uploads/support-attachments/{fileName}";
        }
    }
}
