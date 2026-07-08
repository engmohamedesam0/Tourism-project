using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Services
{
    public interface INotificationService
    {
        void ScanAndCreate(int sponsorId);
        int GetUnreadCount(int sponsorId);
        List<Notification> GetNotifications(int sponsorId, bool? isRead = null);
        bool MarkAsRead(int notificationId, int sponsorId);
        void MarkAllRead(int sponsorId);
    }

    public class NotificationService : INotificationService
    {
        private readonly TouristContext _context;

        public NotificationService(TouristContext context)
        {
            _context = context;
        }

        public void ScanAndCreate(int sponsorId)
        {
            var now = DateTime.Now;
            var created = false;

            var sponsorRedemptions = _context.Redemptions
                .Include(r => r.Reward)
                .Include(r => r.Tourist)
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
                        Type = "RewardRedeemed",
                        Message = message,
                        IsRead = false
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
                        Type = "RewardExpired",
                        Message = message,
                        IsRead = false
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
                        Type = "RewardLowStock",
                        Message = message,
                        IsRead = false
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
    }
}
