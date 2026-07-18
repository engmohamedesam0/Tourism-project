# Fix: New/edited destinations never appear on the ArcGIS map

## Context
`Tourist_Project_MVC/Controllers/DestinationController.cs` saves destinations to the app DB
(synchronously via `_repo`) but fires the ArcGIS sync with fire-and-forget (`_ = ...`).
Because the POST `Create`/`Edit` actions return `IActionResult` (not `async`), nothing awaits the
sync. ASP.NET Core tears down the request scope on the `RedirectToAction`, killing the in-flight
ArcGIS `applyEdits` network call. The destination therefore shows in any DB-backed view but never
reliably reaches the FeatureServer the map queries. No error surfaces because the task is unobserved.

The sync service (`Services/ArcGISSyncService.cs`) is correct — **do not modify it**.

## Fix
In `DestinationController.cs`, make both POST actions `async Task<IActionResult>` and `await` the
sync before redirecting. Change ONLY the signature, the `async` modifier, and replace
`_ = _arcgisSync.SyncDestinationsAsync(...)` with `await _arcgisSync.SyncDestinationsAsync(...)`.
Keep all validation, `Location` construction, `ModelState.Remove`, `ViewBag` assignments, and
`RedirectToAction("Index")` exactly as-is. (`System.Threading.Tasks` is already imported, line 8.)

### 1) Create action (lines 130-151)
```
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Destination destination, [Range(-90, 90)] double Lat, [Range(-180, 180)] double Long)
{
    destination.Location = new Point(Long, Lat) { SRID = 4326 };
    ModelState.Remove("Location");

    if (ModelState.IsValid)
    {
        destination.Visits = 0;
        _repo.Add(destination);
        _repo.Save();
        await _arcgisSync.SyncDestinationsAsync(new[] { destination });
        return RedirectToAction("Index");
    }
    ViewBag.Lat = Lat;
    ViewBag.Long = Long;
    return View(destination);
}
```

### 2) Edit action (lines 161-180)
- Signature: `public async Task<IActionResult> Edit(...)`
- Replace line 176: `_ = _arcgisSync.SyncDestinationsAsync(new[] { destination });`
  with `await _arcgisSync.SyncDestinationsAsync(new[] { destination });`

## Risks / notes
- The POST now blocks on the real ArcGIS network call before the 302. Slightly longer latency is
  expected and correct, not a regression.
- A failed sync will now surface as an exception on the request (HTTP 500) instead of silently
  dropping. That is the intended behavior to make failures visible.

## Validation
1. As admin, create a new destination → immediately open Explore/Trip map (whichever queries the
   Destinations layer); it should appear without manually refreshing the ArcGIS service.
2. Edit an existing destination's location → confirm its position updates on the map.
3. Build the project (`dotnet build`) to confirm the controller still compiles.
