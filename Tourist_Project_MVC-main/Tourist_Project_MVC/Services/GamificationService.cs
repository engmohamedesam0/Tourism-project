using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;
using Tourist_Project_MVC.Repositories;

namespace Tourist_Project_MVC.Services
{
    public class GamificationService : IGamificationService
    {
        private readonly TouristContext _context;
        private readonly IBadgeRepository _badgeRepo;
        private readonly IUserBadgeRepository _userBadgeRepo;
        private readonly IUserProgressRepository _progressRepo;

        public GamificationService(
            TouristContext context,
            IBadgeRepository badgeRepo,
            IUserBadgeRepository userBadgeRepo,
            IUserProgressRepository progressRepo)
        {
            _context = context;
            _badgeRepo = badgeRepo;
            _userBadgeRepo = userBadgeRepo;
            _progressRepo = progressRepo;
        }

        public async Task<(int XPAdded, List<Badge> NewBadges)> AwardXPAsync(int touristId, int xp, string source)
        {
            var progress = await _progressRepo.GetOrCreateForTouristAsync(touristId);
            progress.CurrentXP += xp;

            var newLevel = LevelDefinitions.GetLevel(progress.CurrentXP);
            progress.CurrentLevel = newLevel.Level;

            var qualifyingBadges = _badgeRepo.GetBadgesForLevel(progress.CurrentXP, progress.CurrentLevel);
            var earnedBadgeIds = _userBadgeRepo.GetByTouristId(touristId).Select(ub => ub.BadgeId).ToHashSet();

            var newBadges = new List<Badge>();
            foreach (var badge in qualifyingBadges)
            {
                if (!earnedBadgeIds.Contains(badge.Id))
                {
                    var userBadge = new UserBadge
                    {
                        TouristId = touristId,
                        BadgeId = badge.Id,
                        EarnedAt = DateTime.Now,
                        IsFeatured = false
                    };
                    _context.UserBadges.Add(userBadge);
                    newBadges.Add(badge);
                }
            }

            if (newBadges.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
            else
            {
                _context.UserProgress.Update(progress);
                await _context.SaveChangesAsync();
            }

            return (xp, newBadges);
        }

        public async Task<UserProgress> GetOrInitializeProgressAsync(int touristId)
        {
            return await _progressRepo.GetOrCreateForTouristAsync(touristId);
        }

        public async Task<List<UserBadge>> GetBadgesForTouristAsync(int touristId)
        {
            return _userBadgeRepo.GetByTouristId(touristId);
        }

        public async Task<(int XPAdded, List<Badge> NewBadges)> AwardXPAndSaveAsync(int touristId, int xp, string source)
        {
            return await AwardXPAsync(touristId, xp, source);
        }
    }
}