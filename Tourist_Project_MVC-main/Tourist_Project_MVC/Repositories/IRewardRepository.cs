using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface IRewardRepository : IRepository<Reward>
    {
        IEnumerable<Reward> GetFiltered(string? search, string? rewardType);
    }
}