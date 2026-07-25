# Fix: Missing TripPlanId in HandleAddDestinationToolCall

## Context

`HandleAddDestinationToolCall` in `AiChatService.cs` creates a new `TripDestination` (`newStop`) and passes it to `_tripPlanRepo.AddStop(newStop)`, but the `TripPlanId` FK property is never set. `TripDestination.TripPlanId` is required by the database schema. This causes a `DbUpdateException` (FK violation) on `_tripPlanRepo.Save()`, which crashes as an unhandled 500 instead of returning a graceful chat reply.

## Fix

In `Tourist_Project_MVC/Services/AiChatService.cs`, inside `HandleAddDestinationToolCall`, add `TripPlanId = trip.Id` to the `newStop` initializer:

```csharp
var newStop = new TripDestination
{
    TripPlanId = trip.Id,
    DestinationId = args.DestinationId,
    Visit_Order = maxOrder + 1,
    ArrivalDate = trip.StartDate,
    DepartureDate = trip.EndDate
};
```

No other changes to this method or to `HandleRemoveDestinationToolCall` / `HandleReorderDestinationsToolCall` — those are unaffected (RemoveStop/UpdateStop use existing tracked entities with FK already populated; ReorderDestinations only updates `Visit_Order` on existing `TripDestination` rows fetched via `GetByIdWithDetails`).

## Verification

1. Rebuild: `dotnet build` should succeed with 0 errors
2. Existing tests (none for this specific flow) — confirm no regressions
3. Manual verification: trigger add-destination tool call via chat → should return a graceful reply instead of 500