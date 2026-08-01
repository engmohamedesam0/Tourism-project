using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class FavoriteButtonVM
    {
        public Tourist_Project_MVC.Models.FavoriteItemType ItemType { get; set; }
        public int ItemId { get; set; }
        public bool IsFavorited { get; set; }
    }
}
