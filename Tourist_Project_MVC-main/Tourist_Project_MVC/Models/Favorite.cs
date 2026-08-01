namespace Tourist_Project_MVC.Models
{
    public class Favorite
    {
        public int Id { get; set; }

        public int TouristId { get; set; }
        public Tourist? Tourist { get; set; }

        public FavoriteItemType ItemType { get; set; }
        public int ItemId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
