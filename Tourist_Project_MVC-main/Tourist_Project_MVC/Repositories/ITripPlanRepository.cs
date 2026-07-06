using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ITripPlanRepository : IRepository<TripPlan>
    {
        IEnumerable<TripPlan> GetAllWithDetails();
        TripPlan? GetByIdWithDetails(int id);
        void RemoveTripDestinations(int tripPlanId);
        IEnumerable<TripPlan> GetFiltered(string? search, int? touristId);
    }
}