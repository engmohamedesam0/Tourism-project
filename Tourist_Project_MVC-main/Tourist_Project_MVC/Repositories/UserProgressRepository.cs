using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class UserProgressRepository : Repository<UserProgress>, IUserProgressRepository
    {
        public UserProgressRepository(TouristContext context) : base(context) { }

        public UserProgress? GetByTouristId(int touristId)
        {
            return _context.UserProgress
                .FirstOrDefault(up => up.TouristId == touristId);
        }

        public async Task<UserProgress> GetOrCreateForTouristAsync(int touristId)
        {
            var progress = GetByTouristId(touristId);
            if (progress != null) return progress;

            progress = new UserProgress
            {
                TouristId = touristId,
                CurrentXP = 0,
                CurrentLevel = 1,
                CompletedTrips = 0,
                CompletedMissions = 0,
                VisitedPlaces = 0,
                UploadedPhotos = 0,
                ReviewsCount = 0,
                LoginStreak = 0,
                LastLoginDate = null
            };

            Add(progress);
            await _context.SaveChangesAsync();
            return progress;
        }
    }
}