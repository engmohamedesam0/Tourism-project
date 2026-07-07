using Tourist_Project_MVC.Models;

namespace Tourist_Project_MVC.Repositories
{
    public interface ITripPlanRepository : IRepository<TripPlan>
    {
        IEnumerable<TripPlan> GetAllWithDetails();
        TripPlan? GetByIdWithDetails(int id);
        TripPlan? GetDraftTrip(int touristId);
        TripDestination? GetTripDestination(int tripDestinationId);
        void AddStop(TripDestination stop);
        void UpdateStop(TripDestination stop);
        void RemoveStop(int tripDestinationId);
        void RemoveTripDestinations(int tripPlanId);
        IEnumerable<TripPlan> GetFiltered(string? search, int? touristId);
    }
}