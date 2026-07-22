using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminSupportController : Controller
    {
        private readonly ISupportTicketService _ticketService;
        private readonly INotificationService _notificationService;
        private readonly TouristContext _context;

        public AdminSupportController(
            ISupportTicketService ticketService,
            INotificationService notificationService,
            TouristContext context)
        {
            _ticketService = ticketService;
            _notificationService = notificationService;
            _context = context;
        }

        // All support tickets across sponsors and tourists, with search + filters.
        public IActionResult Index(string? status, string? category, string? search, string? submitterType)
        {
            var tickets = _ticketService.GetAll();

            var sponsorIds = tickets.Where(t => t.SponsorId.HasValue).Select(t => t.SponsorId.Value).Distinct().ToList();
            var touristIds = tickets.Where(t => t.TouristId.HasValue).Select(t => t.TouristId!.Value).Distinct().ToList();

            var sponsorNames = _context.Sponsors
                .Where(s => sponsorIds.Contains(s.Id))
                .ToDictionary(s => s.Id, s => s.Name);

            var touristNames = _context.Tourists
                .Where(t => touristIds.Contains(t.Id))
                .ToDictionary(t => t.Id, t => t.Name);

            if (!string.IsNullOrEmpty(status))
                tickets = tickets.Where(t => t.Status == status).ToList();

            if (!string.IsNullOrEmpty(category))
                tickets = tickets.Where(t => t.Category == category).ToList();

            if (!string.IsNullOrEmpty(submitterType))
            {
                if (submitterType == "Sponsor")
                    tickets = tickets.Where(t => t.SponsorId.HasValue && !t.TouristId.HasValue).ToList();
                else if (submitterType == "Tourist")
                    tickets = tickets.Where(t => t.TouristId.HasValue && !t.SponsorId.HasValue).ToList();
                else if (submitterType == "Tourist -> Sponsor")
                    tickets = tickets.Where(t => t.TouristId.HasValue && t.SponsorId.HasValue).ToList();
            }

            if (!string.IsNullOrEmpty(search))
                tickets = tickets
                    .Where(t => (t.Subject != null && t.Subject.Contains(search, StringComparison.OrdinalIgnoreCase))
                                || (t.Description != null && t.Description.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            var rows = tickets.Select(t => new AdminSupportTicketRow
            {
                Ticket = t,
                SubmitterType = t.TouristId.HasValue ? (t.SponsorId.HasValue ? "Tourist -> Sponsor" : "Tourist") : "Sponsor",
                SubmitterName = t.TouristId.HasValue
                    ? (touristNames.TryGetValue(t.TouristId.Value, out var tName) ? tName : "Unknown Tourist")
                    : (sponsorNames.TryGetValue(t.SponsorId.Value, out var sName) ? sName : "Unknown Sponsor"),
                RoutedSponsorName = t.SponsorId.HasValue && sponsorNames.TryGetValue(t.SponsorId.Value, out var rName) ? rName : null
            }).ToList();

            var vm = new AdminSupportIndexVM
            {
                Tickets = rows,
                StatusFilter = status,
                CategoryFilter = category,
                Search = search,
                SubmitterTypeFilter = submitterType,
                Categories = SupportTicketVM.Categories,
                Statuses = new List<string> { "Open", "In Progress", "Resolved" }
            };

            // Top stat-box row (real aggregates, query-level).
            var now = DateTime.Now;
            var totalTickets = _context.SupportTickets.Count();
            var resolvedThisMonth = _context.SupportTickets.Count(t =>
                t.Status == "Resolved" &&
                t.RespondedDate.HasValue &&
                t.RespondedDate.Value.Year == now.Year &&
                t.RespondedDate.Value.Month == now.Month);
            var sponsorTickets = _context.SupportTickets.Count(t => t.SponsorId.HasValue && !t.TouristId.HasValue);

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-ticket-detailed-fill", Color = "blue", Value = totalTickets.ToString("N0"), Label = "Total Tickets" },
                new StatBoxItem { IconClass = "bi-envelope-open-fill", Color = "amber", Value = _context.SupportTickets.Count(t => t.Status == "Open").ToString("N0"), Label = "Open Tickets" },
                new StatBoxItem { IconClass = "bi-check2-circle", Color = "green", Value = resolvedThisMonth.ToString("N0"), Label = "Resolved This Month" },
                new StatBoxItem { IconClass = "bi-building-fill", Color = "purple", Value = sponsorTickets.ToString("N0"), Label = "Sponsor Tickets" }
            };

            return View(vm);
        }

        public IActionResult Details(int id)
        {
            var ticket = _ticketService.GetByIdForAdmin(id);
            if (ticket == null) return NotFound();

            string submitterName;
            string submitterType;

            if (ticket.TouristId.HasValue)
            {
                var tourist = _context.Tourists.FirstOrDefault(t => t.Id == ticket.TouristId.Value);
                submitterName = tourist?.Name ?? "Unknown Tourist";
                submitterType = ticket.SponsorId.HasValue ? "Tourist -> Sponsor" : "Tourist";
            }
            else
            {
                var submitterSponsor = _context.Sponsors.FirstOrDefault(s => s.Id == ticket.SponsorId!.Value);
                submitterName = submitterSponsor?.Name ?? "Unknown Sponsor";
                submitterType = "Sponsor";
            }

            var adminName = ticket.RespondedByAdminId != null
                ? _context.Users.FirstOrDefault(u => u.Id == ticket.RespondedByAdminId)?.UserName
                : null;

            var sponsor = ticket.SponsorId.HasValue
                ? _context.Sponsors.FirstOrDefault(s => s.Id == ticket.SponsorId.Value)
                : null;

            var vm = new AdminSupportDetailsVM
            {
                Ticket = ticket,
                SubmitterName = submitterName,
                SubmitterType = submitterType,
                RespondedByAdminName = adminName,
                Categories = SupportTicketVM.Categories,
                SponsorResponse = ticket.SponsorResponse,
                SponsorRespondedDate = ticket.SponsorRespondedDate,
                SponsorName = sponsor?.Name
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Respond(int id, string adminResponse)
        {
            var ticket = _ticketService.GetByIdForAdmin(id);
            if (ticket == null) return NotFound();

            if (string.IsNullOrWhiteSpace(adminResponse))
            {
                ModelState.AddModelError("adminResponse", "A response message is required.");
                return RedirectToAction("Details", new { id });
            }

            ticket.AdminResponse = adminResponse.Trim();
            ticket.RespondedDate = DateTime.Now;
            ticket.RespondedByAdminId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

            // First admin touch moves an Open ticket into In Progress.
            if (ticket.Status == "Open")
                ticket.Status = "In Progress";

            _ticketService.Update(ticket);

            if (!ticket.TouristId.HasValue)
            {
                _notificationService.Create(
                    ticket.SponsorId!.Value,
                    "SupportResponse",
                    $"An admin responded to your support ticket: \"{ticket.Subject}\".",
                    "SupportTicket",
                    ticket.Id);
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Resolve(int id)
        {
            var ticket = _ticketService.GetByIdForAdmin(id);
            if (ticket == null) return NotFound();

            ticket.Status = "Resolved";
            _ticketService.Update(ticket);

            return RedirectToAction("Details", new { id });
        }
    }
}
