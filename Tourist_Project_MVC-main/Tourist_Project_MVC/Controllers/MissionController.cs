using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Security.Claims;
using Tourist_Project_MVC.Controllers.HubNotifications;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;
using Tourist_Project_MVC.Services;
using Tourist_Project_MVC.View_Model;
using Tourist_Project_MVC.DTOs;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Tourist_Project_MVC.Controllers
{
    [Authorize(Roles = "User,Admin")]
    public class MissionController : Controller
    {
        private readonly IMissionRepository missionRepo;
        private readonly IDestinationRepository destRepo;
        private readonly TouristContext _context;
        private readonly IGamificationService _gamificationService;
        private readonly IHubContext<NotificationHub> _hubContext;
        public MissionController(
            IMissionRepository missionRepo,
            IDestinationRepository destRepo,
            TouristContext context,
            IGamificationService gamificationService,
            IHubContext<NotificationHub> hubContext)
        {
            this.missionRepo = missionRepo;
            this.destRepo = destRepo;
            _context = context;
            _gamificationService = gamificationService;
            _hubContext = hubContext;
        }

        public IActionResult Index(string? search, string? missionType)
        {
            var missions = missionRepo.GetAll();

            // Filter
            ViewBag.AllCount = missions.Count();
            ViewBag.MissionTypes = missions
                .Select(m => m.MissionType)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            ViewBag.Search = search;
            ViewBag.MissionType = missionType;

            if (!string.IsNullOrEmpty(search))
                missions = missions.Where(m =>
                    m.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    m.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(missionType))
                missions = missions.Where(m => m.MissionType == missionType);

            // Top stat-box row (real aggregates, query-level).
            var total = _context.Missions.Count();
            var destinationsCovered = _context.Missions.Select(m => m.DestinationId).Distinct().Count();
            var avgPoints = total > 0 ? Math.Round(_context.Missions.Average(m => m.PointsReward)) : 0;
            var missionTypes = _context.Missions.Select(m => m.MissionType).Distinct().Count();

            ViewBag.StatBoxes = new List<StatBoxItem>
            {
                new StatBoxItem { IconClass = "bi-flag-fill", Color = "blue", Value = total.ToString("N0"), Label = "Total Missions" },
                new StatBoxItem { IconClass = "bi-geo-alt-fill", Color = "green", Value = destinationsCovered.ToString("N0"), Label = "Destinations Covered" },
                new StatBoxItem { IconClass = "bi-stars", Color = "gold", Value = avgPoints.ToString("N0"), Label = "Avg Points Reward" },
                new StatBoxItem { IconClass = "bi-collection", Color = "purple", Value = missionTypes.ToString("N0"), Label = "Mission Types" }
            };

            return View(missions);
        }

        public IActionResult Details(int id)
        {
            Mission mission = missionRepo.GetById(id);
            if (mission != null)
            {
                return View(mission);
            }
            return NotFound();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            MissionWithDeptListVM mission = new MissionWithDeptListVM();
            List<Destination> destinations = destRepo.GetAll().ToList();
            mission.destinations = destinations;
            return View(mission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(MissionWithDeptListVM missionFromReq)
        {
            if (ModelState.IsValid)
            {
                Mission mission = new()
                {
                    Title = missionFromReq.Title,
                    MissionType = missionFromReq.MissionType,
                    Description = missionFromReq.Description,
                    PointsReward = missionFromReq.PointsReward,
                    DestinationId = missionFromReq.DestinationId
                };
                missionRepo.Add(mission);
                missionRepo.Save();

                await _hubContext.Clients.All.SendAsync("MissionAdded", new MissionDTO
                {
                    Id = mission.Id,
                    Title = mission.Title,
                    Desc = mission.Description,
                    Points = mission.PointsReward,
                    MissDestId = mission.DestinationId,
                    Type = mission.MissionType
                });
                return RedirectToAction("Index");
            }
            missionFromReq.destinations = destRepo.GetAll().ToList();
            return View(missionFromReq);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id)
        {
            Mission missionFromDb = missionRepo.GetById(id);
            if (missionFromDb == null)
            {
                return NotFound();
            }
            MissionWithDeptListVM mission = new()
            {
                Id = missionFromDb.Id,
                Title = missionFromDb.Title,
                MissionType = missionFromDb.MissionType,
                PointsReward = missionFromDb.PointsReward,
                Description = missionFromDb.Description,
                DestinationId = missionFromDb.DestinationId,
            };
            List<Destination> destinations = destRepo.GetAll().ToList();
            mission.destinations = destinations;
            return View(mission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task <IActionResult > Edit(MissionWithDeptListVM missionFromReq)
        {
            if (ModelState.IsValid)
            {
                Mission mission = new()
                {
                    Id = missionFromReq.Id,
                    Title = missionFromReq.Title,
                    MissionType = missionFromReq.MissionType,
                    Description = missionFromReq.Description,
                    PointsReward = missionFromReq.PointsReward,
                    DestinationId = missionFromReq.DestinationId
                };
                missionRepo.Update(mission);
                missionRepo.Save();
                await _hubContext.Clients.All.SendAsync("MissionUpdated", new MissionDTO
                {
                    Id = mission.Id,
                    Title = mission.Title,
                    Desc = mission.Description,
                    Points = mission.PointsReward,
                    MissDestId = mission.DestinationId,
                    Type = mission.MissionType
                });
                return RedirectToAction("Index");
            }
            List<Destination> destinations = destRepo.GetAll().ToList();
            missionFromReq.destinations = destinations;
            return View(missionFromReq);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Delete(int id)
        {
            Mission missionFromDb = missionRepo.GetById(id);
            return View(missionFromDb);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult >DeleteConfirmed(int id)
        {
            missionRepo.Delete(id);
            missionRepo.Save();
            await _hubContext.Clients.All.SendAsync("MissionDeleted", id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> CompleteMission(int missionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var tourist = _context.Tourists.FirstOrDefault(t => t.ApplicationUserId == userId);
            if (tourist == null) return RedirectToAction("Index", "Explore");

            var mission = missionRepo.GetById(missionId);
            if (mission == null) return NotFound();

            var existing = _context.UserMissions
                .FirstOrDefault(um => um.TouristId == tourist.Id && um.MissionId == missionId);

            if (existing != null && existing.Status == "Completed")
            {
                TempData["MissionMessage"] = "You have already completed this mission.";
                TempData["MissionMessageType"] = "warning";
                return RedirectToAction("Details", new { id = missionId });
            }

            if (existing == null)
            {
                existing = new UserMission
                {
                    TouristId = tourist.Id,
                    MissionId = missionId,
                    Status = "Completed",
                    PointsEarned = mission.PointsReward,
                    Completed_At = DateTime.Now
                };
                _context.UserMissions.Add(existing);
            }
            else
            {
                existing.Status = "Completed";
                existing.PointsEarned = mission.PointsReward;
                existing.Completed_At = DateTime.Now;
                _context.UserMissions.Update(existing);
            }

            tourist.point_Balance += mission.PointsReward;
            _context.Tourists.Update(tourist);

            await _context.SaveChangesAsync();

            var (xpAdded, newBadges) = await _gamificationService.AwardXPAsync(tourist.Id, 50, "mission-complete");

            TempData["MissionMessage"] = $"Mission completed! +{mission.PointsReward} points, +{xpAdded} XP";
            TempData["MissionMessageType"] = "success";
            TempData["NewBadges"] = newBadges?.Select(b => b.Name).ToList();
            TempData["NewBadgesIcon"] = newBadges?.Select(b => b.Icon).ToList();

            return RedirectToAction("Details", new { id = missionId });
        }
    }
}