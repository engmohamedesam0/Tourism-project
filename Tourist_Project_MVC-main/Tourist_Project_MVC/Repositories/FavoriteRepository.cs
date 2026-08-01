using Microsoft.EntityFrameworkCore;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public class FavoriteRepository : Repository<Favorite>, IFavoriteRepository
    {
        public FavoriteRepository(TouristContext context) : base(context) { }

        public bool IsFavorited(int touristId, FavoriteItemType itemType, int itemId) =>
            _context.Favorites.Any(f => f.TouristId == touristId &&
                f.ItemType == itemType && f.ItemId == itemId);

        public Favorite? Find(int touristId, FavoriteItemType itemType, int itemId) =>
            _context.Favorites.FirstOrDefault(f => f.TouristId == touristId &&
                f.ItemType == itemType && f.ItemId == itemId);

        public IEnumerable<Favorite> GetByTourist(int touristId, FavoriteItemType? itemType = null)
        {
            var query = _context.Favorites.AsQueryable()
                .Where(f => f.TouristId == touristId);

            if (itemType.HasValue)
                query = query.Where(f => f.ItemType == itemType.Value);

            return query.OrderByDescending(f => f.CreatedAt).ToList();
        }

        public HashSet<int> GetFavoritedItemIds(int touristId, FavoriteItemType itemType) =>
            _context.Favorites
                .Where(f => f.TouristId == touristId && f.ItemType == itemType)
                .Select(f => f.ItemId)
                .ToHashSet();
    }
}
