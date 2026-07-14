# Wire up real Leaflet maps via GeoServer WFS (proxy) layers

## CORS decision (flagged)
**Solved via a server-side proxy** (`MapController`), not GeoServer CORS config. Browser JS never touches `localhost:8080` directly — it calls same-origin MVC actions that fetch the WFS response server-side. This sidesteps CORS entirely and is the more resilient option requested.

## 1. Configuration
- **`appsettings.json`** — add a `GeoServer` section (no hardcoded `localhost:8080` in JS/views):
  ```json
  "GeoServer": {
    "BaseUrl": "http://localhost:8080/geoserver/EGYEXPLORE/ows",
    "DestinationsTypeName": "EGYEXPLORE:Destinations",
    "BranchesTypeName": "EGYEXPLORE:Branches",
    "MaxFeatures": 50
  }
  ```
- **`Program.cs`** — register `builder.Services.AddHttpClient();` (for `IHttpClientFactory` used by `MapController`).

## 2. Server-side WFS proxy — `Controllers/MapController.cs` (new)
- Inject `IHttpClientFactory` + `IConfiguration`.
- `GetDestinationsGeoJson()` and `GetBranchesGeoJson()` (HTTP GET):
  - Build WFS URL from config: `{BaseUrl}?service=WFS&version=1.0.0&request=GetFeature&typeName={TypeName}&outputFormat=application/json&maxFeatures={MaxFeatures}`.
  - `await client.GetAsync(...)`, read `ResponseContent`, return `Content(geojson, "application/json")` on 200; on failure return empty `FeatureCollection` JSON (`{"type":"FeatureCollection","features":[]}`) with 200 so the frontend can distinguish "empty" from "crash" cleanly.
- Route: default MVC (`/Map/GetDestinationsGeoJson`, `/Map/GetBranchesGeoJson`).

## 3. Leaflet via CDN (consistent with existing CDN libs)
- **`_Layout.cshtml`**: add `leaflet.css` in `<head>` (cdnjs `…/leaflet/1.9.4/leaflet.css`); add `leaflet.js` **immediately before** `@await RenderSectionAsync("Scripts", required: false)` (line ~860) so `L` exists before each page's `@section Scripts` runs.

## 4. Reusable JS — `wwwroot/js/maps.js` (new, vanilla JS, no jQuery)
Exposes a global `EGYMaps` with defensive init + `invalidateSize()` after load.

- **`initWfsMap({ mapEl, proxyUrl, propMap, popupHtml, markerStyle, onLayerReady })`** — read-only maps:
  - OSM tile layer (`https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`) **always** added first.
  - `fetch(proxyUrl)` → `L.geoJSON(...)` added on top. **On any failure**: keep basemap, inject a small inline notice (`⚠ Couldn't load live layer`) into `mapEl`, never throw.
  - `propMap` (config object) maps feature → `id/name/city/category` with fallbacks (`properties.id ?? properties.Id ?? feature.id`, etc.). `popupHtml` builds the popup incl. a Details link (`/Destination/Details/{id}` or `/NearMe/Details/{sponsorId}`).
  - Returns the map + the geojson layer so callers can filter markers (Explore/Trip).

- **`initLocationPicker({ mapEl, latInput, lngInput, proxyUrl, contextStyle, initialLat, initialLng })`** — reused by **both** Admin Destination and Sponsor Branch forms (single implementation):
  - OSM tiles + optional **context** WFS layer (read-only, dimmed `contextStyle`) so the admin/sponsor sees existing points while placing a new one.
  - A **draggable** marker at `(initialLat, initialLng)` if provided.
  - `map.on('click', e => { setMarker(e.latlng); writeInputs(e.latlng); })` — click drops/moves marker.
  - `latInput/lngInput` `input` listeners → parse & `marker.setLatLng(...)` (and `panTo`). Round to 6 decimals in both directions.
  - Defensive: context WFS failure still shows notice but picker stays fully functional.

## 5. Read-only map pages
- **Explore** (`Views/Explore/Index.cshtml`): replace `#exploreMap` watermark with a real map (keep `id="exploreMap"`). `initWfsMap` with `/Map/GetDestinationsGeoJson`; popup shows name/city/category + Details link. **Nice-to-have (implement if straightforward):** filter-chip sync — give each marker the card's `data-category`/`data-status` and hide/show markers when a chip is clicked, mirroring the existing list filter.
- **Trip builder** (`Views/Trip/Index.cshtml`): replace `#tripBuilderMap` watermark with `initWfsMap` (Destinations). Highlight the stops currently checked in the picker as a distinct styled overlay (read `data-lat`/`data-lng` from `.picker-row`; nice-to-have).
- **Trip Details** (`Views/Trip/Details.cshtml`): replace `#tripDetailsMap` watermark. `initWfsMap` Destinations as **dimmed background**, then overlay the trip's own stops from `li[data-lat][data-lng]` as **distinct highlighted markers** (different color/icon + order number from `.stop-order-badge`), fit bounds to all stops.
- **Near Me** (`Views/NearMe/Index.cshtml`): replace the ArcGIS `<iframe>` in `#nearbyMap` with `initWfsMap` using `/Map/GetBranchesGeoJson`; popup shows branch/sponsor name + link to `/NearMe/Details/{sponsorId}` (per confirmed decision).

## 6. Location-picker forms (reusable component from §4)
- **Admin Destination** `Create.cshtml` & `Edit.cshtml`: add `id="Lat"`/`id="Long"` to the two number inputs (they already post as `Lat`/`Long`). Add a map container `#destinationMap` near the inputs. Call `initLocationPicker` with Destinations WFS context; `initialLat/Lng` from `@Model.Location.Y`/`@Model.Location.X` on Edit, blank on Create. **No controller change** — `DestinationController.Create/Edit` already build `new Point(Long, Lat)` from `Lat`/`Long`.
- **Sponsor Branch** `Create.cshtml` & `Edit.cshtml`: `asp-for="Lat"`/`asp-for="Long"` already yield `id="Lat"`/`id="Long"`. Add map container `#branchMap`; call `initLocationPicker` with Branches WFS context. **Fix pre-existing gap:** set `value="@Model.Lat"`/`value="@Model.Long"` on the Edit inputs (currently blank → 0). **No controller change** — `SponsorBranchController` already builds `new Point(vm.Long, vm.Lat)`.
- Add CSS height for `#destinationMap`/`#branchMap` (~360px) so Leaflet renders.

## Constraints respected
- **No data-model changes** — `Destination.Location` / `Branch.Location` Point construction untouched; maps are a UI layer only.
- Map init is defensive everywhere (basemap + notice on WFS failure).

## Open risk / verify during build
- **GeoServer feature property names** (esp. the primary-key field used for the Details link) must be confirmed against the running instance and plugged into `propMap`. If the PK isn't exposed, fall back to matching by name/coords. Adjust `propMap` accordingly after a live check.

## Validation
1. `dotnet build` succeeds.
2. With GeoServer running: Explore, Trip builder, Trip Details, Near Me all render OSM + live features; popups/links work.
3. With GeoServer **down**: each page shows basemap + inline notice, no broken page.
4. Admin Destination form: click-to-place updates Lat/Long; typing Lat/Long moves marker (both directions). Sponsor Branch form: same, both directions. Context layers render.
5. Existing save flows still persist `Location` correctly.
