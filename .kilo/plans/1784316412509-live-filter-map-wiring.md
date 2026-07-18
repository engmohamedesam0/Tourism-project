# Plan: Trip map markers + NearMe closure verification + Egypt zoom check

## IMPORTANT: repo-state correction (read first)
The request states PART B (NearMe) is "confirmed NOT yet applied (repo still on the same commit)." **This contradicts the actual file.** A fresh read of `Views/NearMe/Index.cshtml` shows both NearMe sub-fixes are already present:
- Lines 620-631: `nearMeState` + `setNearMeChip` are declared **inside** the IIFE, with `window.setNearMeChip = setNearMeChip;` at line 631 (so inline `onclick="setNearMeChip(...)"` resolves it).
- Line 612: `var sid = _firstDefined(f.attributes, nearMePropMap.id, []);` — already the correct **3-argument** form.

So PART B does not require further edits. Re-applying it (moving global declarations that are already correctly placed inside the IIFE) would only risk breaking the page. The plan therefore treats PART B as **verification-only**. If, after pulling/cloning on the target machine, the file is found reverted to the broken state (global `setNearMeChip` at lines ~524-535, IIFE starting at ~537), then perform the move described in the "PART B (if reverted)" section.

## PART A — Trip: destinations never appear on the map (REAL FIX)
File: `Views/Trip/Index.cshtml`
- Line 430 (buggy): `function destId(feature) { return String(feature.id || '').split('.').pop(); }`
- `filterMarkers()` (maps.js) calls the predicate with `{ attributes: {...}, properties: {...} }` — there is no top-level `.id`. So `destId` always returns `''`, `rowById['']` misses every row, and `tripPredicate()` (line ~473-478) returns `false` for **all** markers. `applyTripFilter()` runs automatically via `onLayerReady` on load, so every destination marker is hidden from the start.
- Fix: replace line 430 with:
  ```js
  function destId(feature) {
      var p = feature.attributes || feature.properties || {};
      return _firstDefined(p, tripPropMap.id, ['']);
  }
  ```
  - `_firstDefined` is defined at line 423 and `tripPropMap.id` at line 399 (`['id','Id','destination_id']`), both in scope at line 430. Mirror of how `buildPopup()` (line 409) already extracts fields.
- Validation: `rowById` lookups (line 450-453, keyed by `data-dest-id`) now match the feature id; all destination markers appear on initial load with zero filters. Temporary `console.log` of unmatched ids during testing, then remove.

## PART B — NearMe (VERIFY, do not re-edit unless reverted)
File: `Views/NearMe/Index.cshtml`
- Current good state: IIFE opens at line 524; `nearMeState`/`setNearMeChip` + `window.setNearMeChip` inside (620-631); `applyNearMeClientFilter` at 604; `filterMarkers` predicate uses 3-arg `_firstDefined` at 612.
- Action: open the page, DevTools console open, click every type/rating/distance chip and type in search → **zero console errors**, list filters, non-matching markers disappear, map zooms to visible set. If errors appear, see fallback below.

### PART B (if reverted to broken state)
Move the `nearMeState` var + `setNearMeChip` function from global scope into the IIFE, right after `applyNearMeClientFilter` (after line 618), and add `window.setNearMeChip = setNearMeChip;` at the end of the IIFE. Leave `applyNearMeFilter` (lines 510-522) untouched. Also ensure line 612 reads `_firstDefined(f.attributes, nearMePropMap.id, [])` (3 args). Do NOT touch Explore/Index.cshtml or maps.js.

## PART C — Egypt default zoom (CONDITIONAL, likely no change)
File: `wwwroot/js/maps.js`, MapView construction ~line 319:
- Current default: `center: [opts.center ? opts.center[1] : 31.2357, opts.center ? opts.center[0] : 30.0444]` → resolves to `[31.2357, 30.0444]` = [lon, lat] = **Cairo**, `zoom: 7`. This already frames Egypt; none of the three pages pass `opts.center`, so this default is reached.
- Action: leave as-is. Only IF, after PART A/B verification, the initial view still looks too wide, tighten to an Egypt-only extent: `center: [30.8, 26.8], zoom: 6`, and verify visually. Make that edit only if needed.

## Scope
- Files: `Views/Trip/Index.cshtml` (PART A, definitely edit), `Views/NearMe/Index.cshtml` (PART B, verify only unless reverted), `wwwroot/js/maps.js` (PART C, conditional).
- Do NOT modify Explore/Index.cshtml, server controllers, or view models.

## Validation
1. Trip: load `Trip/Index` with console open; all destination markers present with no filters; toggling interests/budget filters shows/hides markers like Explore.
2. NearMe: clean console across all chips + search; markers filter and map zooms.
3. Egypt zoom: initial views frame Egypt reasonably; tighten only if too wide.
4. `dotnet build` still passes (Trip/Explore/NearMe are view-only; maps.js is JS).
