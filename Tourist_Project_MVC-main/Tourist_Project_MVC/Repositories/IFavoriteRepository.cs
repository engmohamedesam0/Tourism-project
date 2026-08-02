using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IFavoriteRepository : IRepository<Favorite>
    {
        bool IsFavorited(int touristId, FavoriteItemType itemType, int itemId);
        Favorite? Find(int touristId, FavoriteItemType itemType, int itemId);
        IEnumerable<Favorite> GetByTourist(int touristId, FavoriteItemType? itemType = null);
        HashSet<int> GetFavoritedItemIds(int touristId, FavoriteItemType itemType);
    }
}
