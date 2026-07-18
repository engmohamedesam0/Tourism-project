# Plan: Wire up the existing ArcGIS API key to the browser

## Context
Both maps render fully blank with **zero console errors** — a known symptom of anonymous ArcGIS usage being rate-limited and failing silently. The API key already exists in config (`appsettings.json:13`, `ArcGIS:ApiKey`) but is **never returned to the browser** and **never applied** to the ArcGIS SDK. `_ensureApiKey(cfg)` is an empty no-op, and its two call sites don't `await` it. This plan wires the key end-to-end so authenticated requests are made before any module import.

Assumption flagged: the blank-map symptom is *likely* caused by missing auth; this fix is correct regardless and is the required first step before re-testing filter interactions.

## Verified state
- `Controllers/MapController.cs:18-22` — `GetMapConfig()` returns only `destinationsLayerUrl` + `branchesLayerUrl`; no `apiKey`.
- `wwwroot/js/maps.js:82-83` — `_ensureApiKey(cfg)` is empty `{}`.
- `wwwroot/js/maps.js:297-298` — main `initWfsMap` flow: `var cfg = await _ensureConfig(); _ensureApiKey(cfg);` (no await), then `EsriMap = await $arcgis.import('@arcgis/core/Map.js')` at line 300.
- `wwwroot/js/maps.js:349-350` — location-picker flow: same pattern, `$arcgis.import` starts at line 352.
- `appsettings.json:13` — `ArcGIS:ApiKey` present (non-empty).

## Changes

### 1) Controllers/MapController.cs — return the API key
Replace the `Json(new { ... })` body (lines 18-22) with:
```csharp
return Json(new
{
    apiKey = _config["ArcGIS:ApiKey"] ?? string.Empty,
    destinationsLayerUrl = _config["ArcGIS:DestinationsLayerUrl"] ?? string.Empty,
    branchesLayerUrl = _config["ArcGIS:BranchesLayerUrl"] ?? string.Empty
});
```
No new usings needed (`IConfiguration` already injected, `string.Empty` already in scope).

### 2) wwwroot/js/maps.js — implement _ensureApiKey (lines 82-83)
Replace the empty no-op with:
```js
async function _ensureApiKey(cfg) {
    if (!cfg || !cfg.apiKey) return;
    var esriConfig = await $arcgis.import('@arcgis/core/config.js');
    esriConfig.apiKey = cfg.apiKey;
}
```

### 3) wwwroot/js/maps.js — await at both call sites
- Line 298 (main flow): change `_ensureApiKey(cfg);` → `await _ensureApiKey(cfg);`
  (this is already inside an `async (function () {...})()` — confirmed the surrounding loader is async, so `await` is valid here.)
- Line 350 (location-picker flow): change `_ensureApiKey(cfg);` → `await _ensureApiKey(cfg);`
  (this loader is also `async (function () {...})()`.)

Both `await`s sit **before** their respective `$arcgis.import('@arcgis/core/Map.js')` calls (lines 300 / 352), so the key is set on `esriConfig` before any request-bearing module import.

## Scope guard
- Only edit `MapController.cs` and `maps.js`.
- Do NOT touch Explore/NearMe/Trip `.cshtml`, `filterMarkers`/`fitBounds`, or server filter logic.

## Validation
1. `dotnet build` passes.
2. Hard-refresh (cache clear) Explore, NearMe, Trip.
3. Confirm markers render on initial load with zero filters and **zero console errors**.
4. Only after confirming blank-map is fixed, re-test the filter interactions (chips/search/bounds) from the prior tasks.

## Risk / open question
- If maps are still blank after this with a clean console, the cause is something else (layer URL/permissions, basemap token, CORS) and needs a fresh look — but the key wiring is mandatory regardless and is the documented next step.
