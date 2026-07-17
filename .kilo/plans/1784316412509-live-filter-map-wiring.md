# Plan: Live filter-panel ↔ ArcGIS map wiring (Explore / NearMe / Trip)

## Context
Three pages render destination/sponsor cards in a left list panel and an ArcGIS map (via `EGYMaps` API in `wwwroot/js/maps.js`) on the right. Today only Explore's **category chip ↔ map** link is correct & reload-free (`explorePredicate` + `syncMarkersFromChips`, `Views/Explore/Index.cshtml:452-505`). The goals:
- Make every filter panel (chips, checkboxes, search boxes) update the paired map live, without full page reloads.
- Honor the constraint: **do NOT modify `maps.js`, the map layer/config setup, or server controllers.** Only client-side filtering/zooming using the existing `EGYMaps` API (`filterMarkers`, `view.goTo`, `fitBounds`).

## Existing API surface (maps.js) — DO NOT CHANGE
- `mapInstance.filterMarkers(predicate)` — `(feature, graphic)` where `feature = { attributes, properties }`; sets `graphic.visible`. (`maps.js:108`)
- `mapInstance.fitBounds(latlngs)` — array of `[lat, lng]`; computes an Extent in WGS84 and `view.goTo`. No-op if empty/undefined. (`maps.js:150`)
- `mapInstance.view.goTo(...)` / `mapInstance.overlayLayer()` / `mapInstance.view.openPopup()` — used by existing card-click handlers (keep intact).

## Shared rules (all three pages)
1. Debounce text-input filtering 150–250ms (use ~200ms).
2. After a filter change, if **≥1 visible card** remains, call `fitBounds()` over the visible cards' `data-lat`/`data-lng`. If **0 visible**, do NOT call `fitBounds` — leave map as-is and show the page's existing empty-state element.
3. Never break: marker popups, "click a card → pan + open popup", or each file's `onLayerReady` initial sync.
4. No real geocoding — "zoom to searched location" = `fitBounds` over matching cards already in the DOM.

---

## 1) Views/Explore/Index.cshtml
Status: chip↔map already works; search box is the gap.

### 1a. Fold search text into `explorePredicate()` (`Views/Explore/Index.cshtml:452`)
- `explorePredicate` currently only reads the active chip. Add search-term awareness: read the search input value once (`document.getElementById('exploreSearch').value.trim().toLowerCase()`). A feature passes if it matches the chip rule **AND** (no query OR its name/category contains the query).
  - The feature's name/category are in `feature.properties` via `explorePropMap` (`name`/`category` keys). Use `_firstDefined` so it stays consistent with the rest of the file.
- `applyFilter()` (`Views/Explore/Index.cshtml:469`) currently hides/shows cards by chip only. Make the **card list** filtering the single source of truth: a card is visible if `showByChip(card)` **AND** `showBySearch(card)`. Apply this combined visibility in `applyFilter()` and in the search `keyup` handler so list + map always agree.
  - `syncMarkersFromChips()` still calls `filterMarkers(explorePredicate)` — now the predicate also encodes the search term, so the map follows the list automatically.

### 1b. Remove full-reload Enter behavior (`Views/Explore/Index.cshtml:520-524`)
- Delete the `keypress` Enter → `exploreSearchForm.submit()` block.
- Replace with Enter → blur the input (`searchInput.blur()`), no-op otherwise. Keep the `<form>` markup but it should no longer be submitted by the user.

### 1c. fitBounds on visible cards after any filter change
- Add helper `fitExploreBounds()`:
  - Collect `data-lat`/`data-lng` from `#exploreList .explore-card:not([style*="display: none"])` (visible cards; or iterate `cards` and check `style.display !== 'none'`).
  - If `latlngs.length === 0` → return (leave map, empty state already toggled by `toggleExploreEmpty`).
  - Else `mapInstance.fitBounds(latlngs)`.
- Call `fitExploreBounds()` at the end of `applyFilter()` and at the end of the search `keyup` handler (and the `all` chip branch at `Views/Explore/Index.cshtml:497-503`).
- Keep `syncMarkersFromChips()` + `toggleExploreEmpty()` calls; add the bounds call after them.

---

## 2) Views/NearMe/Index.cshtml
Status: chips currently call `applyNearMeFilter(key,value)` → `form.submit()` (full reload, `Views/NearMe/Index.cshtml:506-517`). Search box + Go submit the form (full reload). Map `onLayerReady` currently hides markers not in the server-rendered card list (`Views/NearMe/Index.cshtml:559-567`).

### 2a. Add filter attributes to each `.nearme-card` (Razor, `Views/NearMe/Index.cshtml:112-116`)
- Current attributes: `data-id`, `data-lat`, `data-lng`, `data-name`.
- Add `data-type="@card.Sponsor.Type"` and `data-rating="@((int)Math.Round(card.AvgRating))"` (mirror the `avg` var already computed at line 111).
- `data-distance` is NOT needed: distance is already baked into the server dataset and the card order; client distance chip filtering is out of scope per spec (only type/rating/distance chips → the distance chip filters by the `DistanceKm` already rendered). **Decision:** the spec lists "type, rating, and distance chips" to convert. Implement distance client-side too using `data-distance="@card.DistanceKm"` (store the km value) so the `≤ N km` chip can filter in-browser. Add that attribute as well.

### 2b. Replace `applyNearMeFilter` reload with client-side filtering (`Views/NearMe/Index.cshtml:506-517`)
- Keep a global `nearMeState = { type: null, rating: null, distance: null }` (init from `Model.Type/Model.Rating/Model.Distance` so a server reload preserves state).
- Rewrite chip `onclick` handlers to set `nearMeState[key]` and call a new `applyNearMeClientFilter()` instead of submitting.
  - Simplest: change the inline `onclick="applyNearMeFilter('type','Hotel')"` to `onclick="setNearMeChip('type','Hotel')"` (new function), and `applyNearMeFilter` can remain as the server-submit path **only** for `destinationId`.
- `applyNearMeClientFilter()`:
  - For each `.nearme-card`: visible if `(!type || data-type===type)` && `(!rating || Number(data-rating) >= Number(rating))` && `(!distance || Number(data-distance) <= Number(distance))`.
  - Set `card.style.display`.
  - Call `nearMeMap.filterMarkers(pred)` where `pred(feature)` reads `data-id` of each visible card (reuse the existing onLayerReady id-collection approach) — or simpler, build the visible id set and match against `feature.attributes` sponsor id via `nearMePropMap.id` keys.
  - Toggle NearMe empty state (add `<div id="nearMeEmptyFilter" ...>` mirror of Explore's `#exploreEmptyFilter`, show when 0 visible).
  - Call `fitNearMeBounds()` (only if ≥1 visible).
- Wire chip active-class toggling (move `chip-active` off siblings, onto clicked chip) inside `setNearMeChip`.

### 2c. Live search (`Views/NearMe/Index.cshtml:39-46`)
- The search `<input name="search">` + Go button currently submit the form (server). **Keep the Go button + destinationId as server round-trips** (they change the dataset).
- Add a debounced (`keyup`, ~200ms) listener on the search input that ALSO filters client-side (independent of the Go submit):
  - Query `q = value.trim().toLowerCase()`; show card if `!q || data-name.toLowerCase().includes(q) || data-type.toLowerCase().includes(q)`.
  - Combine with the chip state: a card is visible only if it passes BOTH the chip filter and the search filter. So factor visibility into one `cardVisible(card, state, q)` helper used by both chip apply and search apply.
  - After updating display: `nearMeMap.filterMarkers(...)` + `fitNearMeBounds()` (+ empty toggle).
- The "Go" button keeps `type="submit"` → server reload. After that reload the DOM already reflects the new dataset; see 2d for re-applying client filters.

### 2d. Re-apply client filters after server reload (`Views/NearMe/Index.cshtml:519` IIFE)
- Initialize `nearMeState` from the URL query string (`new URLSearchParams(location.search)`): `type`, `rating`, `distance`, `search`.
- After map `onLayerReady` (or at IIFE end, guarded until map ready), call `applyNearMeClientFilter()` using `nearMeState` + the `search` value so the client filters persist across the destinationId/Go round-trip, then `fitNearMeBounds()`.
- Keep the existing `onLayerReady` initial sync (visible-id → `filterMarkers`) but have it also run `applyNearMeClientFilter()` afterwards so chip/rating/distance/search state is enforced on first paint.

---

## 3) Views/Trip/Index.cshtml
Status: `applyTripFilter()` filters `.picker-row` and already calls `tripMap.filterMarkers(tripPredicate)` (`Views/Trip/Index.cshtml:583`), but **no fitBounds after filtering**, and picker rows **lack `data-lat`/`data-lng`** even though `refresh()` reads them at lines 505-506 (so today any fitBounds would get `NaN`).

### 3a. Add coordinates to TripStopVM + controller + Razor
- `View Model/TripBuilderVM.cs` (`TripStopVM`, line 28): add `public double Lat { get; set; }` and `public double Lng { get; set; }`.
- `Controllers/TripController.cs` (`Index`, line 83-93): populate `Lat = d.Location.Y`, `Lng = d.Location.X` in the `.Select(...)` (Destination `Location` is a `NetTopologySuite.Geometries.Point`; `Y`=lat, `X`=lng — matches Explore's usage at `Views/Explore/Index.cshtml:55-56`).
- `Views/Trip/Index.cshtml` picker row (`Views/Trip/Index.cshtml:172-176`): add `data-lat="@Model.Stops[i].Lat"` and `data-lng="@Model.Stops[i].Lng"`.

### 3b. fitBounds after `applyTripFilter()` (`Views/Trip/Index.cshtml:549-585`)
- Add `fitTripBounds()`: collect `[lat,lng]` from visible `.picker-row` (`:visible`, or iterate and check `$(this).css('display')!=='none'`), parse `data-lat`/`data-lng`, skip NaN.
  - If 0 visible → return (do NOT fit; `#tripEmptyFilter` already toggled by `toggleTripEmpty()`).
  - Else `tripMap.fitBounds(latlngs)`.
- Call `fitTripBounds()` at the end of `applyTripFilter()` (after `toggleTripEmpty()`).
- The existing `tripPredicate`/`filterMarkers` wiring stays as-is (markers already match the picker list). Verify `tripPredicate` uses `rowById` + `rowMatches` (it does, line 473-478) so markers already match exactly.
- Keep `onLayerReady: applyTripFilter` (line 436) — now also fits bounds on initial load over all rows.

---

## Risk / open notes
- **Trip coords were missing** (lines 505-506 referenced undefined `data-lat`/`data-lng`). 3a is mandatory for `fitBounds` to work; without it bounds are NaN. This is the only change touching a ViewModel + controller, but it's view-model plumbing only — not "server-side controller logic" for filtering, so it's within scope.
- NearMe `distance` client filtering requires `data-distance` from `DistanceKm`; if the team prefers distance to remain server-only, drop the distance attribute and keep that chip as a server submit — but spec explicitly lists distance chips for client conversion, so include it.
- Keep `applyNearMeFilter` name only as the server-submit helper for `destinationId`/`Go`; rename the client path to avoid confusion.
- All `fitBounds` calls must guard `length === 0` to avoid leaving the map blank/zoomed-out.

## Validation
- Explore: type in search box → list + map markers filter live, no reload; Enter blurs without submit; chip change + search both refit bounds when results exist, hold map when none.
- NearMe: click type/rating/distance chips → list + map update live, no reload; type in search → live filter; change destinationId / press Go → server reload, then client chip/rating/distance/search re-apply and bounds refit.
- Trip: check interest checkboxes / set budget → picker list + map markers both filter; bounds refit over visible rows; 0 matches shows `#tripEmptyFilter` and leaves map.
- Run `dotnet build` (or the repo's lint/build command) to ensure the VM/controller edits compile.
