# Plan: Fix ArcGIS Feature Layer Sync for Destinations and Branches

## Current State Verified
- `ArcGISSyncService.cs` uses C# property names verbatim in `applyEdits` attributes (e.g. `Id`, `Name`, `latitude`).
- Interface returns `Task`; callers get no result.
- All failures log `LogWarning` only; nothing surfaced to UI.
- `SponsorBranchController` POST actions use `_ =` fire-and-forget on sync `IActionResult` methods.
- Only 2 callers each of `SyncDestinationsAsync` and `SyncBranchesAsync` (controllers).
- `DestinationController` awaits correctly; `SponsorBranchController` does not.
- No `ArcGISSyncResult` type exists.
- No field-schema fetching exists.
- Views lack TempData alerts for map sync status.
- `TouristReward/Index.cshtml` already shows the exact Bootstrap alert pattern to reuse.

## Implementation Tasks (ordered)

### 1. `Services/ArcGISSyncService.cs`
- Add `public record ArcGISSyncResult(bool Success, string? Error, int AddedCount, int UpdatedCount);` with helper constructors: `Success()` and `Fail(string error)`.
- Change interface signatures to `Task<ArcGISSyncResult>`.
- Add a static `ConcurrentDictionary<string, Dictionary<string,string>>` + `SemaphoreSlim` for layer field-schema cache keyed by layer URL.
- Before building adds/updates, `GET {layerUrl}?f=json&token=...` and build a case-insensitive field-name map (`StringComparer.OrdinalIgnoreCase`). Cache it.
- Resolve every attribute key through the map; fall back to the logical name if the map is unavailable.
- Update `QueryObjectIdAsync` to accept a resolved `idFieldName` parameter and use it in the `where` clause instead of hardcoding `"Id"`.
- Change all genuine failure `LogWarning` to `LogError` and return `ArcGISSyncResult.Fail(...)` with extracted ArcGIS error messages (`error.description` / `error.message` from per-feature results).
- Preserve: not-configured early return as `Success()`, form-encoded `applyEdits` POST, `spatialReference: { wkid: 4326 }`, `/0` layer handling.

### 2. `Controllers/DestinationController.cs`
- Capture `var result = await _arcgisSync.SyncDestinationsAsync(...)` in Create and Edit POST.
- On `!result.Success`, set `TempData["DestinationMessage"] = $"Destination was saved, but the ArcGIS map sync failed: {result.Error}"` and `TempData["DestinationMessageType"] = "danger"`.
- On success, set `TempData["DestinationMessage"] = "Destination saved successfully."` and `TempData["DestinationMessageType"] = "success"`.
- Redirect to Index in both cases.

### 3. `Controllers/SponsorBranchController.cs`
- Change Create and Edit POST signatures to `async Task<IActionResult>`.
- Replace `_ = _arcgisSync.SyncBranchesAsync(...)` with `var result = await _arcgisSync.SyncBranchesAsync(...)`.
- On `!result.Success`, set `TempData["BranchMessage"]` / `TempData["BranchMessageType"] = "danger"` the same way.
- Redirect to Index in both cases.

### 4. Views
- `Views/Destination/Index.cshtml`: add dismissible Bootstrap alert block right after `<h1>` matching `TouristReward/Index.cshtml` pattern (`DestinationMessage` / `DestinationMessageType`).
- `Views/SponsorBranch/Index.cshtml`: add same pattern (`BranchMessage` / `BranchMessageType`) right after `<h2>`.

### 5. Build & Compile Fixes
- Search entire solution for any other `SyncDestinationsAsync` / `SyncBranchesAsync` callers (already verified only controllers, but re-run grep after changes).
- Fix any compile errors from interface change.

## Risks / Validation
- If ArcGIS layer is query-only (CSV-published default), `applyEdits` will return an error about capabilities; this is portal-side and cannot be code-fixed.
- Field casing: the schema fetch resolves this, but we log the raw field list for debugging.
- "Not configured" remains a silent success to avoid false alerts.

## Out of Scope
- DB schema changes, model changes, layer URL/config changes.
- Changing editing capabilities on the ArcGIS Online portal.
