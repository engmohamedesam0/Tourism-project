using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Services
{
    public interface INotificationService
    {
        void ScanAndCreate(int sponsorId);
        void ScanAndCreateForAdmin();

        // Sponsor-scoped (kept for the existing sponsor notification pages).
        int GetUnreadCount(int sponsorId);
        List<Notification> GetNotifications(int sponsorId, bool? isRead = null);
        bool MarkAsRead(int notificationId, int sponsorId);
        void MarkAllRead(int sponsorId);
        bool Delete(int notificationId, int sponsorId);

        // Recipient-aware (Admin / Sponsor / Tourist).
        int GetUnreadCount(string role, int? sponsorId, string? userId);
        List<Notification> GetNotifications(string role, int? sponsorId, string? userId, bool? isRead = null);
        bool MarkAsRead(int notificationId, string role, int? sponsorId, string? userId);
        void MarkAllRead(string role, int? sponsorId, string? userId);
        bool Delete(int notificationId, string role, int? sponsorId, string? userId);

        void Create(int sponsorId, string type, string message, string? relatedEntityType = null, int? relatedEntityId = null);
        void CreateForUser(string role, string? userId, string type, string message, string? relatedEntityType = null, int? relatedEntityId = null);

        // Maps the signed-in user to the recipient they should see.
        (string Role, int? SponsorId, string? UserId) ResolveRecipient(ClaimsPrincipal user, ISponsorRepository sponsorRepo);
    }

    public class NotificationService : INotificationService
    {
        private readonly TouristContext _context;

        public NotificationService(TouristContext context)
        {
            _context = context;
        }

        public (string Role, int? SponsorId, string? UserId) ResolveRecipient(ClaimsPrincipal user, ISponsorRepository sponsorRepo)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (user.IsInRole("Sponsor"))
            {
                var sponsorId = sponsorRepo.GetAll()
                    .Where(s => s.ApplicationUserId == userId)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefault() ?? 0;
                return ("Sponsor", sponsorId == 0 ? null : sponsorId, userId);
            }

            if (user.IsInRole("Admin"))
                return ("Admin", null, userId);

            return ("Tourist", null, userId);
        }

        // ---- Sponsor-scoped (legacy) ----

        public void ScanAndCreate(int sponsorId)
        {
            var now = DateTime.Now;
            var created = false;

            var sponsorRedemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Include(r => r.Tourist)
                    .ThenInclude(t => t.ApplicationUser)
                .Where(r => r.Reward != null && r.Reward.SponsorId == sponsorId)
                .ToList();

            foreach (var redemption in sponsorRedemptions)
            {
                var message = $"Reward \"{redemption.Reward!.Title}\" redeemed by {(redemption.Tourist != null ? redemption.Tourist.Name : "a tourist")} on {redemption.RedemptionDate:yyyy-MM-dd}.";
                if (!_context.Notifications.Any(n => n.SponsorId == sponsorId && n.Type == "RewardRedeemed" && n.Message == message))
                {
                    _context.Notifications.Add(new Notification
                    {
                        SponsorId = sponsorId,
                        RecipientRole = "Sponsor",
                        Type = "RewardRedeemed",
                        Message = message,
                        IsRead = false,
                        RelatedEntityType = "Redemption",
                        RelatedEntityId = redemption.Id
                    });
                    created = true;
                }
            }

            var expiredRewards = _context.Rewards
                .Where(r => r.SponsorId == sponsorId && r.Status == "Active" && r.ExpirationDate < now)
                .ToList();

            foreach (var reward in expiredRewards)
            {
                var message = $"Reward \"{reward.Title}\" expired on {reward.ExpirationDate:yyyy-MM-dd}.";
                if (!_context.Notifications.Any(n => n.SponsorId == sponsorId && n.Type == "RewardExpired" && n.Message == message))
                {
                _context.Notifications.Add(new Notification
                {
                    SponsorId = sponsorId,
                    RecipientRole = "Sponsor",
                    Type = "RewardExpired",
                    Message = message,
                    IsRead = false,
                    RelatedEntityType = "Reward",
                    RelatedEntityId = reward.Id
                });
                    created = true;
                }
            }

            var lowStockRewards = _context.Rewards
                .Where(r => r.SponsorId == sponsorId && r.Status == "Active" && r.QuantityAvailable == 0)
                .ToList();

            foreach (var reward in lowStockRewards)
            {
                var message = $"Reward \"{reward.Title}\" is out of stock.";
                if (!_context.Notifications.Any(n => n.SponsorId == sponsorId && n.Type == "RewardLowStock" && n.Message == message))
                {
                _context.Notifications.Add(new Notification
                {
                    SponsorId = sponsorId,
                    RecipientRole = "Sponsor",
                    Type = "RewardLowStock",
                    Message = message,
                    IsRead = false,
                    RelatedEntityType = "Reward",
                    RelatedEntityId = reward.Id
                });
                    created = true;
                }
            }

            if (created)
                _context.SaveChanges();
        }

        public int GetUnreadCount(int sponsorId)
        {
            return _context.Notifications.Count(n => n.SponsorId == sponsorId && !n.IsRead);
        }

        public List<Notification> GetNotifications(int sponsorId, bool? isRead = null)
        {
            var query = _context.Notifications
                .Where(n => n.SponsorId == sponsorId);

            if (isRead.HasValue)
                query = query.Where(n => n.IsRead == isRead.Value);

            return query
                .OrderByDescending(n => n.CreatedDate)
                .ToList();
        }

        public bool MarkAsRead(int notificationId, int sponsorId)
        {
            var notification = _context.Notifications
                .FirstOrDefault(n => n.Id == notificationId && n.SponsorId == sponsorId);

            if (notification == null) return false;

            notification.IsRead = true;
            _context.SaveChanges();
            return true;
        }

        public void MarkAllRead(int sponsorId)
        {
            var unread = _context.Notifications
                .Where(n => n.SponsorId == sponsorId && !n.IsRead)
                .ToList();

            foreach (var n in unread)
                n.IsRead = true;

            if (unread.Any())
                _context.SaveChanges();
        }

        public void Create(int sponsorId, string type, string message, string? relatedEntityType = null, int? relatedEntityId = null)
        {
            _context.Notifications.Add(new Notification
            {
                SponsorId = sponsorId,
                RecipientRole = "Sponsor",
                Type = type,
                Message = message,
                IsRead = false,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId
            });
            _context.SaveChanges();
        }

        public bool Delete(int notificationId, int sponsorId)
        {
            var notification = _context.Notifications
                .FirstOrDefault(n => n.Id == notificationId && n.SponsorId == sponsorId);

            if (notification == null) return false;

            _context.Notifications.Remove(notification);
            _context.SaveChanges();
            return true;
        }

        // ---- Recipient-aware (Admin / Sponsor / Tourist) ----

        public void ScanAndCreateForAdmin()
        {
            var created = false;

            // Notify admins about sponsor registrations still waiting for approval.
            var pending = _context.SponsorApprovalRequests
                .Where(r => r.Status == "Pending")
                .ToList();
            foreach (var req in pending)
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == req.ApplicationUserId);
                var name = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "a new sponsor";
                var message = $"New sponsor registration pending approval: {name}.";

                if (!_context.Notifications.Any(n =>
                    n.RecipientRole == "Admin" && n.Type == "NewSponsorApproval" &&
                    n.RelatedEntityType == "SponsorApproval" && n.RelatedEntityId == req.Id))
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientRole = "Admin",
                        Type = "NewSponsorApproval",
                        Message = message,
                        RelatedEntityType = "SponsorApproval",
                        RelatedEntityId = req.Id
                    });
                    created = true;
                }
            }

            // Notify admins about support tickets that still need attention.
            var openTickets = _context.SupportTickets
                .Where(t => t.Status != "Resolved")
                .ToList();
            foreach (var ticket in openTickets)
            {
                string from;
                if (ticket.TouristId.HasValue)
                {
                    var tourist = _context.Tourists.FirstOrDefault(t => t.Id == ticket.TouristId.Value);
                    from = tourist != null ? $"tourist {tourist.Name}" : "a tourist";
                }
                else
                {
                    var sponsor = ticket.SponsorId.HasValue
                        ? _context.Sponsors.FirstOrDefault(s => s.Id == ticket.SponsorId.Value)
                        : null;
                    from = sponsor != null ? $"sponsor \"{sponsor.Name}\"" : "a user";
                }
                var message = $"New support ticket from {from}: \"{ticket.Subject}\".";

                if (!_context.Notifications.Any(n =>
                    n.RecipientRole == "Admin" && n.Type == "NewSupportTicket" &&
                    n.RelatedEntityType == "SupportTicket" && n.RelatedEntityId == ticket.Id))
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientRole = "Admin",
                        Type = "NewSupportTicket",
                        Message = message,
                        RelatedEntityType = "SupportTicket",
                        RelatedEntityId = ticket.Id
                    });
                    created = true;
                }
            }

            if (created)
                _context.SaveChanges();
        }

        private IQueryable<Notification> ForRecipient(IQueryable<Notification> query, string role, int? sponsorId, string? userId)
        {
            switch (role)
            {
                case "Sponsor":
                    // Legacy rows have RecipientRole == null and belong to a sponsor.
                    return query.Where(n => n.SponsorId == sponsorId
                        && (n.RecipientRole == null || n.RecipientRole == "Sponsor"));
                case "Admin":
                    // Role-wide (RecipientUserId == null) plus notifications addressed
                    // directly to this admin.
                    return query.Where(n => n.RecipientRole == "Admin"
                        && (n.RecipientUserId == null || n.RecipientUserId == userId));
                default: // "Tourist"
                    return query.Where(n => n.RecipientRole == "Tourist" && n.RecipientUserId == userId);
            }
        }

        public int GetUnreadCount(string role, int? sponsorId, string? userId)
        {
            return ForRecipient(_context.Notifications, role, sponsorId, userId)
                .Count(n => !n.IsRead);
        }

        public List<Notification> GetNotifications(string role, int? sponsorId, string? userId, bool? isRead = null)
        {
            var query = ForRecipient(_context.Notifications, role, sponsorId, userId);

            if (isRead.HasValue)
                query = query.Where(n => n.IsRead == isRead.Value);

            return query
                .OrderByDescending(n => n.CreatedDate)
                .ToList();
        }

        public bool MarkAsRead(int notificationId, string role, int? sponsorId, string? userId)
        {
            var notification = ForRecipient(_context.Notifications, role, sponsorId, userId)
                .FirstOrDefault(n => n.Id == notificationId);

            if (notification == null) return false;

            notification.IsRead = true;
            _context.SaveChanges();
            return true;
        }

        public void MarkAllRead(string role, int? sponsorId, string? userId)
        {
            var unread = ForRecipient(_context.Notifications, role, sponsorId, userId)
                .Where(n => !n.IsRead)
                .ToList();

            foreach (var n in unread)
                n.IsRead = true;

            if (unread.Any())
                _context.SaveChanges();
        }

        public bool Delete(int notificationId, string role, int? sponsorId, string? userId)
        {
            var notification = ForRecipient(_context.Notifications, role, sponsorId, userId)
                .FirstOrDefault(n => n.Id == notificationId);

            if (notification == null) return false;

            _context.Notifications.Remove(notification);
            _context.SaveChanges();
            return true;
        }

        public void CreateForUser(string role, string? userId, string type, string message, string? relatedEntityType = null, int? relatedEntityId = null)
        {
            _context.Notifications.Add(new Notification
            {
                SponsorId = null,
                RecipientRole = role,
                RecipientUserId = userId,
                Type = type,
                Message = message,
                IsRead = false,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId
            });
            _context.SaveChanges();
        }
    }
}
