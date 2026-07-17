# Plan: Fix two live-filter-map bugs in maps.js + call sites

Scope correction from prior task: **`wwwroot/js/maps.js` IS in scope now.** Both root causes live there. Call-site edits are also required (Explore debounce).

## Confirmed root causes (verified in code)

### BUG 1 — filterMarkers only affects one marker (filtered items never disappear)
`wwwroot/js/maps.js:245`
```js
graphicsByFeature.set(f.attributes[Object.keys(propMap.id || ['id'])[0] || 'id'], graphic);
```
`Object.keys(propMap.id)` returns array **indices** (`'0','1','2'`), so `f.attributes['0']` is `undefined` for every feature. Every graphic is therefore stored under the same `undefined` key → `graphicsByFeature` (a `Map`) collapses to **1** entry. `filterMarkers` (line 108) iterates that map, so it only ever toggles one graphic → nothing else ever hides.

### BUG 2 — erratic/jumpy zoom when filtering/typing
- **2a:** `Views/Explore/Index.cshtml:558` `fitExploreBounds()` is called directly inside the search `keyup` with **no debounce** (NearMe already debounces at ~200ms). Every keystroke fires a `view.goTo` animation.
- **2b:** `maps.js fitBounds` (line 150-166) computes an `Extent` but has **no degenerate-extent guard**. A single matching marker (or coincident coords) yields `xmin===xmax && ymin===ymax`, making `view.goTo` jump to an extreme/undefined zoom.
- **2c:** `fitBounds` calls `view.goTo(...)` (line 160) and ignores the returned promise. Rapid successive filters create overlapping animations that fight each other; a superseded `goTo` rejects and the code never accounts for that.

---

## Fix 1 — resolve the real ID key (maps.js)
Add a helper near `_firstDefined` (~line 55, after the `_firstDefined` function):
```js
function _firstKey(attrs, keys) {
    for (var i = 0; i < keys.length; i++) {
        if (attrs[keys[i]] !== undefined) return keys[i];
    }
    return keys[0];
}
```
Replace line 245 with:
```js
var idKey = _firstKey(f.attributes, propMap.id || ['id']);
graphicsByFeature.set(f.attributes[idKey], graphic);
```
Validation: after this, `graphicsByFeature.size` must equal `result.features.length` for all three layers:
- Explore → `destinations` layer
- NearMe → `branches` layer
- Trip → `destinations` layer
No longer collapses to 1. `filterMarkers` then toggles every real graphic.

## Fix 2a — debounce Explore search (Views/Explore/Index.cshtml)
In the search `keyup` handler (lines 549-558), wrap the **bounds-triggering** work in a ~200ms debounce, mirroring NearMe. Keep DOM/list filtering immediate (cheap), debounce only the map fit:
```js
var q = getExploreSearch();
cards.forEach(...);                 // immediate list update
syncMarkersFromChips();             // immediate marker hide/show (filterMarkers is cheap)
toggleExploreEmpty();
clearTimeout(searchFitTimer);
searchFitTimer = setTimeout(fitExploreBounds, 200);
```
Declare `var searchFitTimer;` near the top of the IIFE. (Do NOT debounce `syncMarkersFromChips` — marker visibility should track the list live per the prior task; only the `goTo` animation is what needs debouncing.)

## Fix 2b — degenerate-extent guard (maps.js fitBounds)
After computing `extent` (line 159) and before `view.goTo`, add:
```js
var EPS = 0.001;
var xmin = extent[0], ymin = extent[1], xmax = extent[2], ymax = extent[3];
if (xmax - xmin < EPS && ymax - ymin < EPS) {
    view.goTo({ center: [xmin, ymin], zoom: 14 }, { duration: 1000 });
    return;
}
```
This makes a single-result (or coincident-coordinates) filter zoom to a sane, consistent level (zoom 14) instead of an extreme one. Multi-point extents still use the `Extent` target as before.

## Fix 2c — clean supersede of in-flight goTo (maps.js fitBounds)
Store the last `goTo` promise on the closure and let a new call supersede it. ArcGIS cancels a superseded `goTo`; ignore its rejection so it never breaks later calls:
```js
var goToPromise = view.goTo({ target: new Extent({...}) }, { duration: 1000, padding: {...} });
_lastFitPromise = goToPromise;
goToPromise.catch(function (err) {
    // Superseded/aborted goTo rejects by design; ignore unless it's our own subsequent call.
    if (goToPromise !== _lastFitPromise) return;
});
```
Declare `var _lastFitPromise;` in the closure scope (near `var map, view, ...` at line 95). Same pattern applies if the 2b single-point branch is taken (store its promise too). Do **not** touch the card-click `view.goTo` handlers in the views (out of scope; user-initiated pans).

---

## Files to edit
1. `wwwroot/js/maps.js`
   - add `_firstKey` helper (~line 55)
   - replace line 245 `graphicsByFeature.set(...)` 
   - rewrite `fitBounds` (lines 150-166) with 2b guard + 2c promise handling
2. `Views/Explore/Index.cshtml`
   - debounce the `fitExploreBounds()` call in the search `keyup` (lines 549-558), add `var searchFitTimer;`

## Out of scope / unchanged
- `filterMarkers` predicate logic, `onLayerReady` callbacks, card-click pan/popup handlers in the three views, NearMe/Trip debounce (already correct), server controllers, `ViewBag.Search` initial populate.

## Validation (post-fix)
- All three pages: filter to a single category (Temples on Explore; one sponsor type on NearMe; one interest on Trip) → **every** non-matching marker hides, and the map zooms smoothly to fit only the matching markers.
- `graphicsByFeature.size === result.features.length` on each page (verify via console / debugger; should be > 1 for real datasets).
- Typing quickly in Explore search → no oscillation/jump; `goTo` animations supersede cleanly; no uncaught promise rejections in console.
- Single-result filter → zoom settles at a sane level (not extreme).
- `dotnet build` still passes (no C# changes here, but confirm nothing else broke).
