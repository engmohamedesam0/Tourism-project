# Plan: Fix 5 confirmed map-rendering bugs after Leaflet → ArcGIS migration

Commit under test: `4773a13`. No maps render. Root causes identified from code inspection.

---

## Bug 1 — `appsettings.json` placeholders + missing user-secrets wiring

**Current state**: `appsettings.json` has `REPLACE_WITH_*` text (not valid JSON empty strings). `Program.cs` does **not** call `AddUserSecrets`, so even if the developer runs `dotnet user-secrets set ...`, the values will never be loaded into config. `appsettings.Development.json` is tracked in git despite `.gitignore` listing it.

**Fixes**:
1. `appsettings.json`: change `"ApiKey": "REPLACE_WITH_VALID_KEY"` → `"ApiKey": ""`, same for the two URLs → `""`. These are safe-to-commit empty placeholders.
2. `Program.cs`: add `builder.AddUserSecrets<Program>();` right after `var builder = WebApplication.CreateBuilder(args);` (unconditional call; it only activates in Development environment automatically).
3. Git: from repo root, run `git rm --cached Tourist_Project_MVC/appsettings.Development.json`. Leave the file on disk (it only has a DB connection string, no ArcGIS key). Commit the removal so it stops being tracked.
4. Developer runs locally:
   ```
   cd Tourist_Project_MVC
   dotnet user-secrets init
   dotnet user-secrets set "ArcGIS:ApiKey" "<real key>"
   dotnet user-secrets set "ArcGIS:DestinationsLayerUrl" "<real Destinations FeatureServer URL>"
   dotnet user-secrets set "ArcGIS:BranchesLayerUrl" "<real Branches FeatureServer URL>"
   ```

---

## Bug 2 — Views pass dead WFS proxy URLs as `layer`/`contextLayer`

**Current state**: All 8 view call sites still pass `/Map/GetDestinationsGeoJson` or `/Map/GetBranchesGeoJson`. That endpoint was removed. `maps.js` `layerUrlFor()` treats the string as a literal URL, so it tries to fetch a dead proxy.

**Fixes**:
1. `wwwroot/js/maps.js` — `layerUrlFor()` in `initWfsMap` and the equivalent in `initLocationPicker`:
   - Treat `'destinations'` → `cfg.destinationsLayerUrl + '/0'`
   - Treat `'branches'` → `cfg.branchesLayerUrl + '/0'`
   - Reject any other bare string (return `null` so the layer silently doesn't load rather than 404-spamming).
   - `/0` is the hosted-feature-service layer index (single CSV publish → always layer 0; confirmed by standard AGOL behavior).
2. Update all 8 view call sites:
   - `Explore/Index.cshtml`: `layer: '/Map/GetDestinationsGeoJson'` → `layer: 'destinations'`
   - `Trip/Index.cshtml`: same
   - `Trip/Details.cshtml`: same
   - `NearMe/Index.cshtml`: `layer: '/Map/GetBranchesGeoJson'` → `layer: 'branches'`
   - `Destination/Create.cshtml`: `contextLayer: '/Map/GetDestinationsGeoJson'` → `contextLayer: 'destinations'`
   - `Destination/Edit.cshtml`: same
   - `SponsorBranch/Create.cshtml`: `contextLayer: '/Map/GetBranchesGeoJson'` → `contextLayer: 'branches'`
   - `SponsorBranch/Edit.cshtml`: same

---

## Bug 3 — `var Map = (await $arcgis.import('esri/Map')).default` shadows native `Map`

**Current state**: In `maps.js`, both `initWfsMap` (line 90) and `initLocationPicker` (line 293) declare `var Map = (await $arcgis.import('esri/Map')).default`. This shadows the built-in `Map` constructor. Later, `var graphicsByFeature = new Map()` on line 121 constructs an ArcGIS `Map` instance instead of a native JS `Map` dictionary. All subsequent calls (`graphicsByFeature.clear()`, `.set()`, `.forEach()`, `.size`) throw because ArcGIS `Map` doesn't implement that interface. The errors are swallowed by the surrounding `try/catch`, so `onLayerReady` fires with an empty feature set and every map shows the "couldn't load live layer" notice.

**Fix**: Rename the imported class everywhere to `EsriMap` to eliminate shadowing:
- `var EsriMap = (await $arcgis.import('esri/Map')).default;`
- `var map = new EsriMap({ basemap: 'topo-vector' });`
- Do this in **both** `initWfsMap` and `initLocationPicker` so the pattern can't recur.

After this fix, `var graphicsByFeature = new Map()` correctly creates a native JS `Map`.

---

## Bug 4 — `initLocationPicker` context-layer fetch uses dead GeoJSON shape

**Current state**: Lines 369–391 of `maps.js` do `fetch(proxyUrl)` expecting a GeoJSON FeatureCollection with `f.geometry.x`/`.y`. After Bug 2's fix, `proxyUrl` resolves to a FeatureServer layer URL which:
- Doesn't return features from a plain GET (needs `?f=json&token=...`)
- Would need the API key even if it did return data

**Fix**: Replace the fetch block with the same `FeatureLayer` + `queryFeatures()` pattern already used in `initWfsMap`. Reuse the `FeatureLayer`, `Graphic`, `Point`, and `SimpleMarkerSymbol` imports already present at the top of the async IIFE. Query `where: '1=1', outFields: ['*'], returnGeometry: true`, then draw each result as a faint graphic using `ctxStyle`. This gives the same visual outcome (faded context markers) but works correctly with the ArcGIS Online backend.

---

## Bug 5 — `ArcGISSyncService` field names don't match layer schema + missing update logic

**Current state**: `ArcGISSyncService` sends attributes with snake_case keys (`ticket_price`, `contact_number`, `sponsor_id`) and includes non-existent fields (`description`, `tags`). The published layer schema uses PascalCase CSV headers: `Id, Name, City, Category, latitude, longitude, TicketPrice, Rating, Visits, Status` (Destinations) and `Id, SponsorId, Name, Address, latitude, longitude, ContactNumber` (Branches). Additionally, the service only sends `adds` — it never queries for an existing feature by `Id`, so edits to existing Postgres rows never sync to ArcGIS, and re-saving a destination would attempt a duplicate add.

**Fixes**:
1. Rewrite both `SyncDestinationsAsync` and `SyncBranchesAsync` to use the correct field names:
   - Destinations attributes: `Id`, `Name`, `City`, `Category`, `TicketPrice`, `Rating`, `Visits`, `Status` (omit `Description`, `Tags` — they have no corresponding layer field).
   - Branches attributes: `Id`, `SponsorId`, `Name`, `Address`, `ContactNumber`.
2. Add query-then-upsert logic:
   - `GET {layerUrl}/0/query?where=Id={id}&f=json&token={key}` to find existing feature.
   - If found → `applyEdits` with `updates: [{ attributes: { OBJECTID: <existing>, Id: <id>, ... }, geometry: {...} }]`.
   - If not found → `applyEdits` with `adds: [{ attributes: { Id: <id>, ... }, geometry: {...} }]`.
3. Auth: the service currently sets both `X-ESRI-Authorization` header AND `?token=` query param. Remove the header — token-in-query-string is the documented pattern for API-key-authenticated FeatureService REST calls; the header is redundant and could confuse debugging.
4. Keep fail-soft: wrap in try/catch, log warning, return without throwing.

**Note on geometry field names**: The layer has separate `latitude`/`longitude` attribute columns (from the CSV), but ArcGIS `applyEdits` geometry is passed in the `geometry` object (`x`, `y`, `spatialReference.wkid: 4326`). The attribute columns and the geometry are independent — we set both: populate `latitude`/`longitude` attributes for popup display, and pass `geometry` for the actual point position.

---

## Verification steps (run after all fixes)

1. Set real user-secrets locally.
2. Load each map view and confirm markers render with no "couldn't load live layer" notice:
   - `/Explore`
   - `/Trip`
   - `/Trip/Details/{id}` (any trip with stops)
   - `/NearMe`
   - `/Destination/Create`
   - `/SponsorBranch/Create`
3. Create a Destination → confirm it appears in Postgres **and** in the ArcGIS Online Destinations layer within seconds.
4. Edit that Destination → confirm the ArcGIS feature updates (same `OBJECTID`, updated attributes).
5. No `L is not defined` or Leaflet console errors anywhere.
6. `/Map/GetDestinationsGeoJson` and `/Map/GetBranchesGeoJson` return 404.
7. Network tab: API key only sent to `arcgis.com`/`arcgisonline.com` domains.
