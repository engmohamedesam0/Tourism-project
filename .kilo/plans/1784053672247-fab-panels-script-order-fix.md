# Fix: Explore map gone + Trip shows no destination markers (regression from filter↔map sync)

## Root cause (verified)
The "filter↔map sync" refactor (commit `302736a` "Maps-3") rewired Explore, Trip, and Near Me to share `EGYMaps.filterMarkers(predicate)`. The shared helper is fine and its signature is already consistent across all three pages (`filterMarkers(predicate)`). The bug is in the **local** wiring each page does:

- **Explore** — its inline script was rewritten to call `_firstDefined(...)`, `_esc(...)`, and `propMap.id`, but those local definitions were **never added** to Explore (Near Me and Trip each define their own; Explore doesn't). So `initMap()` throws `ReferenceError: propMap is not defined` synchronously while evaluating `EGYMaps.initWfsMap({ propMap: propMap, ... })`. The exception aborts `initMap()` before `L.map(...)` runs → the map never renders at all.
- **Trip** — it *does* define `propMap`/`_firstDefined`/`_esc` locally, but `tripPropMap.id = ['id','Id','destination_id']` does not match the **destinations** GeoJSON. Live `/Map/GetDestinationsGeoJson` returns features with properties `Name, City, Category, Status, ...` and **no id property** — the only id is the GeoJSON top-level `id` string, e.g. `"Destinations.2"` (confirmed by curling the running app). So `_firstDefined(p, tripPropMap.id, [])` → `''`, `tripPredicate` resolves to no `.picker-row` (`rowById[String('')]` undefined) → returns `false` for every feature → `filterMarkers` removes all markers → no destination markers.
- **Near Me works** because the **branches** GeoJSON exposes `SponsorId` inside `properties` (`{"SponsorId":1,...}`, feature id `"Branches.1"`), and `nearMePropMap.id` includes `'SponsorId'`. This confirms the failure is specific to the destinations data/model, not the `filterMarkers` helper.

## Fixes

### No change to Near Me and no change to `maps.js`
`filterMarkers`'s signature is already correct and consistent. Only Explore and Trip need local fixes.

### Explore — `Views/Explore/Index.cshtml` (`@section Scripts`)
Add the missing local definitions (mirror Near Me/Trip) and a `destId(feature)` helper, and use it in `buildPopup`:

1. Inside the IIFE, before `buildPopup`, add:
   ```js
   var propMap = {
       id: ['id', 'Id', 'DestinationId', 'destination_id'],
       name: ['name', 'Name'],
       city: ['city', 'City'],
       category: ['category', 'Category'],
       status: ['status', 'Status'],
       detailsUrl: '/Destination/Details/{id}'
   };

   function _esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
   function _firstDefined(p, keys, fallback) {
       for (var i = 0; i < keys.length; i++) {
           var val = p[keys[i]];
           if (val !== undefined && val !== null && String(val) !== '') return String(val);
       }
       return fallback[0] || '';
   }
   // Destinations GeoJSON has NO id in properties; the id is the feature-level
   // "Destinations.2". Fall back to that so popups/links resolve to a real id.
   function destId(feature) {
       var p = feature.properties || {};
       var id = _firstDefined(p, propMap.id, []);
       if (!id && feature.id) id = String(feature.id).split('.').pop();
       return id;
   }
   ```
2. In `buildPopup`, change `var id = _firstDefined(p, propMap.id, []);` → `var id = destId(feature);`
3. `explorePredicate` is unchanged logically — it already uses `propMap.category` / `propMap.status`, which now exist. No other edits needed.

### Trip — `Views/Trip/Index.cshtml` (`@section Scripts`)
Trip already defines `_esc`/`_firstDefined`/`tripPropMap`; only the id resolution is wrong.

1. Add the same `destId(feature)` helper (use `tripPropMap`):
   ```js
   function destId(feature) {
       var p = feature.properties || {};
       var id = _firstDefined(p, tripPropMap.id, []);
       if (!id && feature.id) id = String(feature.id).split('.').pop();
       return id;
   }
   ```
   (Also add `'DestinationId'` to `tripPropMap.id` for forward-compat — harmless.)
2. In `buildPopup`, change `var id = _firstDefined(p, tripPropMap.id, []);` → `var id = destId(feature);`
3. In `tripPredicate`, change `var id = _firstDefined(p, tripPropMap.id, []);` → `var id = destId(feature);` so `rowById[String(id)]` matches the picker rows' `data-dest-id="@Model.Stops[i].DestinationId"`.

## Why this is safe
- `feature.id` is preserved by `L.geoJSON` and is passed to both `buildPopup(feature, propMap)` and `filterMarkers`'s `predicate(entry.feature, entry.layer)` (see `maps.js` `onEachFeature`/`filterMarkers`). So `destId` works in both places.
- `String(feature.id).split('.').pop()` yields `"2"` from `"Destinations.2"` and is a no-op if a bare id is ever present.
- Near Me is untouched; its `SponsorId`-in-properties lookup already works.

## Validation
1. `dotnet build` → 0 errors.
2. **Explore** (`/Explore`): map renders; category chips (Temples/Museums/Nature/Adventure/Hidden Gems) filter both the list and the map markers; "All" shows all; each marker popup "Details" link is `/Destination/Details/{realId}` (not `//Details/`). No console errors.
3. **Trip** (`/Trip`): map shows the full destinations layer at rest; checking Interest checkboxes and/or setting Budget filters both the picker rows and the map markers (markers whose matching row is hidden are removed by `filterMarkers`); selected-stop blue markers still render and `fitBounds` works. No console errors.
4. **Near Me** (`/NearMe`): unchanged and still works.
5. Cross-check DevTools console on all three pages for zero JS errors.

## Open question
- The destinations GeoJSON exposes no integer `DestinationId` property (only the `"Destinations.N"` feature id). If the GeoServer layer can be configured to publish `DestinationId` in `properties`, the client could read it directly — but the `destId(feature)` fallback above already covers the current data, so no server change is required to fix the regression.
