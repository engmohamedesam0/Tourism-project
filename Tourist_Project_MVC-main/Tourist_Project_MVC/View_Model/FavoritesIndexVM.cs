using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.View_Model
{
    public class FavoritesIndexVM
    {
        public List<Destination> Destinations { get; set; } = new();
        public List<Reward> Rewards { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();
    }
}
