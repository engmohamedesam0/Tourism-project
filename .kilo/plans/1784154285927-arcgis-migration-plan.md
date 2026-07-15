# Plan: Migrate Tourist_Project_MVC from Leaflet to ArcGIS Maps SDK for JavaScript

## 1. Critical blockers that MUST be resolved before implementation

### 1.1 ArcGIS API key is invalid for REST calls
The key provided in the task description returns `{"error":{"code":498,"message":"Invalid token."}}` when used against the FeatureServer REST endpoints. The JS SDK may still accept it (keys can be scoped differently), but **server-side sync will fail** if the key is invalid for REST API calls.
- **Action**: Regenerate a new API key in ArcGIS Online (Agol → My Content → API Keys), ensure it has permissions for the Destinations and Branches feature services, and update `dotnet user-secrets` before wiring the sync service.
- **Risk**: If the key is also invalid for the JS SDK, all maps will fail at runtime with auth errors.

### 1.2 ArcGIS Online layer field schema is unknown
Because the token is invalid, I cannot query `?f=json` to inspect the actual field names. The sync service depends on exact field mappings.
- **Assumption until verified**: Layers were CSV-published and likely have fields `id`, `name`, `city`, `category`, `description`, `ticket_price`, `rating`, `tags`, `visits`, `status`, `opening_hours` (Destinations) and `id`, `name`, `address`, `contact_number`, `sponsor_id` (Branches). Geometry is likely `SHAPE`.
- **Action**: Once a valid token is available, run:
  ```
  curl "https://services8.arcgis.com/Jewkc6qDBXpn2lCJ/arcgis/rest/services/Destinations/FeatureServer/0?f=json&token=YOUR_KEY"
  curl "https://services8.arcgis.com/Jewkc6qDBXpn2lCJ/arcgis/rest/services/Branches/FeatureServer/0?f=json&token=YOUR_KEY"
  ```
  Confirm exact field names, especially the Postgres-Id field name and the geometry field name.

### 1.3 `.gitignore` is broken (merge conflict markers)
Lines 1 and 373-374 contain unresolved git merge markers. This means `appsettings.Development.json` may already be tracked or not properly ignored.
- **Action**: Fix `.gitignore` by removing the conflict markers and ensuring `appsettings.Development.json` is ignored (standard ASP.NET Core template includes it). If `appsettings.Development.json` already contains committed secrets, scrub them from git history or move to user-secrets.

## 2. Phase 1 — Backend

### 2.1 `appsettings.json`
- Remove the entire `"GeoServer"` section.
- Add the `"ArcGIS"` section with empty placeholder values:
  ```json
  "ArcGIS": {
    "ApiKey": "",
    "DestinationsLayerUrl": "https://services8.arcgis.com/Jewkc6qDBXpn2lCJ/arcgis/rest/services/Destinations/FeatureServer",
    "BranchesLayerUrl": "https://services8.arcgis.com/Jewkc6qDBXpn2lCJ/arcgis/rest/services/Branches/FeatureServer"
  }
  ```

### 2.2 `MapController.cs`
- Remove `GetDestinationsGeoJson`, `GetBranchesGeoJson`, and `FetchWfsAsync`.
- Add:
  ```csharp
  [HttpGet]
  public IActionResult GetMapConfig()
  {
      return Json(new {
          apiKey = _config["ArcGIS:ApiKey"],
          destinationsLayerUrl = _config["ArcGIS:DestinationsLayerUrl"],
          branchesLayerUrl = _config["ArcGIS:BranchesLayerUrl"]
      });
  }
  ```
- Keep `IConfiguration` injection. Remove `IHttpClientFactory` from this controller unless needed elsewhere (the sync service will have its own typed client).

### 2.3 New `Services/ArcGISSyncService.cs`
- Interface `IArcGISSyncService` with:
  ```csharp
  Task SyncDestinationAsync(Destination destination, CancellationToken ct = default);
  Task SyncBranchAsync(Branch branch, CancellationToken ct = default);
  ```
- Implementation details:
  - Injected `IHttpClientFactory` + `IConfiguration`.
  - Auth: pass API key as `?token={apiKey}` on every REST call.
  - For each sync:
    1. Query the layer: `GET {layerUrl}/0/query?where={sourceIdField}={id}&f=json&token={apiKey}` to find existing feature by Postgres Id.
    2. If not found → `POST {layerUrl}/0/applyEdits` with `adds: [{ attributes: {...}, geometry: { x, y, spatialReference: { wkid: 4326 } } }]`.
    3. If found → `POST {layerUrl}/0/applyEdits` with `updates: [{ attributes: { OBJECTID: existingObjectId, ... }, geometry: {...} }]`.
  - Field mapping: map Postgres fields to the layer's actual field names (to be confirmed in step 1.2).
  - **Fail soft**: catch all exceptions, log warning, swallow. Never block or roll back the Postgres save.
- Registration in `Program.cs`:
  ```csharp
  builder.Services.AddHttpClient<IArcGISSyncService, ArcGISSyncService>();
  ```

### 2.4 Wire sync into existing save flows
- `DestinationController.cs`:
  - Inject `IArcGISSyncService`.
  - In `Create` POST, after `_repo.Save()` and successful redirect, `await _arcGISSyncService.SyncDestinationAsync(destination);`.
  - Same in `Edit` POST.
- `SponsorBranchController.cs`:
  - Same pattern in `Create` POST and `Edit` POST.
- **Important**: Call sync AFTER the Postgres save succeeds. Do NOT let sync failure change the HTTP response/redirect. Use `_ = Task.Run(() => _arcGISSyncService.SyncDestinationAsync(destination));` or `await` inside a try/catch that only logs.

## 3. Phase 2 — Frontend `wwwroot/js/maps.js`

### 3.1 Architecture
- Keep the IIFE pattern: `var EGYMaps = (function(){...})();`
- Keep the same public API names: `initWfsMap` and `initLocationPicker`.
- Add a module-level config cache:
  ```js
  var _arcgisConfig = null;
  var _arcgisConfigPromise = null;
  function getMapConfig() {
      if (!_arcgisConfigPromise) {
          _arcgisConfigPromise = fetch('/Map/GetMapConfig')
              .then(r => r.ok ? r.json() : Promise.reject(r.status))
              .then(cfg => { _arcgisConfig = cfg; return cfg; })
              .catch(err => { _arcgisConfigPromise = null; throw err; });
      }
      return _arcgisConfigPromise;
  }
  ```
- All map init functions `await getMapConfig()` first, then set `esriConfig.apiKey`.

### 3.2 `initWfsMap(opts)` rewrite
- **New option**: `opts.layer` — `'destinations'` or `'branches'`. Drop `proxyUrl`.
- Map `layer` value to the corresponding URL from config.
- Inside an async IIFE or async function:
  ```js
  const [Map, MapView, FeatureLayer, Graphic, GraphicsLayer, PopupTemplate, SimpleMarkerSymbol, TextSymbol, FeatureFilter] = await $arcgis.import([
      "@arcgis/core/Map.js",
      "@arcgis/core/views/MapView.js",
      "@arcgis/core/layers/FeatureLayer.js",
      "@arcgis/core/Graphic.js",
      "@arcgis/core/layers/GraphicsLayer.js",
      "@arcgis/core/PopupTemplate.js",
      "@arcgis/core/symbols/SimpleMarkerSymbol.js",
      "@arcgis/core/symbols/TextSymbol.js",
      "@arcgis/core/layers/support/FeatureFilter.js"
  ]);
  ```
- Build the `FeatureLayer` with:
  - `url`: from config based on `opts.layer`
  - `popupTemplate`: built from `opts.propMap` using the same `_buildPopupHtml` logic
  - `outFields`: `["*"]`
- Basemap: `"topo-vector"` (closest to current OSM look, consistent across all maps).
- Construct `Map` + `MapView`, attach to `mapEl`.
- On `view.when()`, query features: `layer.queryFeatures({ where: "1=1", outFields: ["*"] })`.
- Store queried features in an array for client-side operations.
- Return shape: `{ map: MapView, layer: () => featureLayer, filterMarkers: function(predicate) { ... } }`.
  - `filterMarkers` implementation: iterate stored graphics, set `graphic.visible = predicate ? predicate(graphic.attributes) : true`. Use a `GraphicsLayer` overlay for visible markers, or toggle `featureLayerView.filter` with a SQL where clause when the predicate can be translated.
  - **Actually**: to keep exact behavior compatibility, maintain a `GraphicsLayer` with all features and toggle `graphic.visible`. This is the safest drop-in replacement for the current Leaflet behavior where `filterMarkers` takes an arbitrary predicate.

Wait — but `featureLayer.queryFeatures()` returns `Feature` objects. We can add them to a `GraphicsLayer` for display, or display them directly via the `FeatureLayer`. The `FeatureLayer` itself renders features. If we want to toggle visibility per feature, we need either:
  a) A `GraphicsLayer` where we control `graphic.visible`
  b) `FeatureLayerView.filter` with a where clause

For arbitrary predicates, option (a) is the only generic solution. So:
- Create a `FeatureLayer` for data source (used for queries).
- Create a `GraphicsLayer` for display.
- On load, copy features from FeatureLayer to GraphicsLayer.
- `filterMarkers(predicate)` iterates GraphicsLayer graphics and sets `visible`.
- `layer()` returns the `FeatureLayer` (for any callers doing `.queryFeatures()` on it).

Actually, looking at current callers:
- Explore: `mapInstance.layer().eachLayer(...)` — this expects a Leaflet GeoJSON layer. It iterates layers to find a matching feature. In ArcGIS, we can instead keep a `Map` of `id → graphic` and look up by id. But the spec says to update Explore's direct Leaflet calls anyway.
- NearMe: `nearMeMap.layer().eachLayer(...)` — same, updated in spec.
- Trip: `tripMap.filterMarkers(tripPredicate)` — keeps `filterMarkers`.
- Trip Details: uses `onLayerReady` only, doesn't use `layer()` or `filterMarkers`.

So the only real `layer()` caller is Explore and NearMe, both of which are being updated. We can simplify `layer()` to return the `FeatureLayer` or the `GraphicsLayer` — doesn't matter much since both callers are getting rewritten.

Let me reconsider: for `filterMarkers`, the cleanest compatible approach is:
- Keep an array `allGraphics` 
- `filterMarkers(predicate)` sets `graphic.visible = predicate(graphic)` for each graphic in a `GraphicsLayer`
- Return `{ map, layer: () => featureLayer, filterMarkers }`

### 3.3 `initLocationPicker(opts)` rewrite
- New option: `opts.contextLayer` — `'destinations'` or `'branches'`. Drop `contextProxyUrl`.
- Load context layer as a second `FeatureLayer` with `opacity: 0.5` and simple `SimpleMarkerSymbol` styling.
- Place a single draggable point graphic:
  - Use `view.on("pointer-down", ...)` to detect if user clicked the marker
  - If yes, set `_dragging = true`, prevent view from panning for that gesture
  - On `view.on("pointer-move", ...)`, if `_dragging`, update graphic geometry: `graphic.geometry = view.toMap(event.x, event.y)` (or `screenPoint`)
  - On `view.on("pointer-up", ...)`, clear `_dragging`, update inputs
  - On `view.on("click", ...)`, if not a drag, move marker to clicked point
- Two-way binding with inputs: same `_round6` logic.
- Return `{ map: MapView, marker: graphic }` (even though callers don't currently use the return value beyond construction).

### 3.4 Error handling and resize
- `_showNotice` stays the same (DOM-based banner).
- Remove manual `invalidateSize` / resize listener — `MapView` auto-resizes via container observers. If Bootstrap tab switches cause issues, call `view.resize()` when the tab becomes visible.

## 4. Phase 3 — `Views/Shared/_Layout.cshtml`

- Remove Leaflet CSS `<link>` (line 20) and Leaflet JS `<script>` (line 892).
- Add ArcGIS CDN in `<head>` (replacing where Leaflet CSS was):
  ```html
  <script type="module" src="https://js.arcgis.com/5.1/"></script>
  ```
- Keep `<script src="~/js/maps.js"></script>` loading AFTER the ArcGIS CDN script and BEFORE any `@section Scripts`.
- No API key embedded in this file.

## 5. Phase 4 — View-specific fixes

### 5.1 `Views/Explore/Index.cshtml`
- Change `proxyUrl: '/Map/GetDestinationsGeoJson'` → `layer: 'destinations'`.
- Replace direct Leaflet internals:
  - `mapInstance.layer().eachLayer(...)` → maintain a client-side `Map` of `id → graphic` (populated from `onLayerReady` features).
  - `layer.openPopup()` → `view.openPopup({ features: [graphic], location: graphic.geometry })`.
  - `mapInstance.map.setView(...)` → `view.goTo({ target: graphic.geometry, zoom: 15 }, { duration: 1000 })`.
  - Keep the fallback to manual `data-lat`/`data-lng` panning.

### 5.2 `Views/Trip/Index.cshtml`
- Change `proxyUrl: '/Map/GetDestinationsGeoJson'` → `layer: 'destinations'`.
- Port `applyTripFilter`/`tripPredicate`/`rowMatches`:
  - Instead of calling `tripMap.filterMarkers(tripPredicate)`, build a `where` clause from matching destination IDs.
  - Collect matching IDs from the DOM (`rowById`, `rowMatches`) and apply `featureLayerView.filter = new FeatureFilter({ where: "id IN (1,2,3)" })`.
  - Rebuild the where clause every time filters change (same trigger points: checkbox change, budget change, filter button click).

### 5.3 `Views/Trip/Details.cshtml`
- Change `proxyUrl: '/Map/GetDestinationsGeoJson'` → `layer: 'destinations'`.
- Replace direct Leaflet calls in `onLayerReady`:
  - `L.circleMarker(...)` → `Graphic` with `SimpleMarkerSymbol` (size ~28px, blue fill `#0d6efd`, white outline).
  - `bindPopup(...)` → `PopupTemplate` per graphic (same "Stop #N" content).
  - `bindTooltip(..., {permanent:true, direction:'center'})` → overlay a `TextSymbol` graphic at the same point showing the stop number, styled centered.
  - `detailsMap.map.fitBounds(latlngs, {padding:[40,40]})` → `view.goTo(stopGraphics, { padding: { top:40, bottom:40, left:40, right:40 } })`.

### 5.4 `Views/NearMe/Index.cshtml`
- Change `proxyUrl: '/Map/GetBranchesGeoJson'` → `layer: 'branches'`.
- Replace direct Leaflet internals in card-click handler:
  - `nearMeMap.layer().eachLayer(...)` → client-side lookup by id.
  - `layer.openPopup()` → `view.openPopup(...)`.
  - `nearMeMap.map.setView(...)` → `view.goTo(...)`.
- Keep `filterMarkers` for initial sponsor visibility filtering (build where clause from visible card IDs).

### 5.5 `Views/Destination/Create.cshtml`, `Edit.cshtml`, `SponsorBranch/Create.cshtml`, `Edit.cshtml`
- Change `contextProxyUrl` → `contextLayer` (`'destinations'` or `'branches'`).
- No other changes needed.

## 6. Phase 5 — Cleanup

- Verify no remaining Leaflet references:
  ```bash
  grep -ri "leaflet\|L\.map\|L\.tileLayer\|L\.geoJSON\|L\.circleMarker\|L\.marker" Tourist_Project_MVC/
  ```
- Confirm `/Map/GetDestinationsGeoJson` and `/Map/GetBranchesGeoJson` return 404.
- Confirm `IHttpClientFactory` is still registered (it is, for the new `ArcGISSyncService` typed client).
- Confirm `GeoServer` section removed from all `appsettings*.json`.
- Fix `.gitignore` merge conflict.
- Add `appsettings.Development.json` to `.gitignore` if not already present.

## 7. Testing checklist (manual, per task spec)

1. Destination Create/Edit: save → verify Postgres save + ArcGIS feature appears/updates.
2. SponsorBranch Create/Edit: same against Branches layer.
3. Explore/Index: map loads, chips filter popup, card click pans+opens popup.
4. NearMe/Index: sponsor markers render, card click pans+opens popup.
5. Trip/Index: trip-builder map renders, interest/budget filters show/hide markers.
6. Trip/Details: numbered stop markers with popups, auto-fit to stops, Sortable.js drag still works.
7. No `L is not defined` console errors.
8. `/Map/GetDestinationsGeoJson` and `/Map/GetBranchesGeoJson` return 404.
9. Network tab: API key only sent to `arcgis.com`/`arcgisonline.com` domains.

## 8. Unresolved / Pre-implementation dependencies

| Item | Owner | Blocking |
|------|-------|----------|
| Valid ArcGIS API key for REST + JS SDK | User | Yes — entire migration fails without it |
| Exact ArcGIS layer field schema | User or implementer (after key fix) | Yes — sync service field mappings |
| `.gitignore` merge conflict resolution | Implementer | No — but should be fixed in same cleanup phase |
