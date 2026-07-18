# Plan: Fix NearMe ReferenceError (chip click) + confirm _firstDefined fallback

## Context
NearMe filter chips throw `Uncaught ReferenceError: applyNearMeClientFilter is not defined at setNearMeChip` the moment any chip is clicked, breaking all client-side filtering on the NearMe map. `applyNearMeFilter` (destinationId dropdown + Go button) is separate and correct — leave it.

## Verified state of the file
`Views/NearMe/Index.cshtml` (718 lines):
- Lines 524-535: `nearMeState` (var) and `setNearMeChip(key, value, chipEl)` are declared **top-level / global** (outside any IIFE).
- Line 526-535 `setNearMeChip` calls `applyNearMeClientFilter()`.
- Lines 537-716: a single `(function () { ... })()` IIFE that defines `applyNearMeClientFilter` (line 617), `nearMeMap` (line 633), `cardMatchesState`, `visibleSponsorIds`, `fitNearMeBounds`, `toggleNearMeEmpty`, and `nearMeState` usage. IIFE closes at line 716.
- 11 inline `onclick="setNearMeChip('type'|'rating'|'distance', ..., this)"` attributes (lines 57-91) — these resolve identifiers only in **global** scope, so `setNearMeChip` must stay reachable globally.
- **BUG 2 status:** line 625 already reads `_firstDefined(f.attributes, nearMePropMap.id, [])` (3 args). The 2-arg version described in the request is **not present** — it was fixed in the previous turn. Keep line 625 as-is; do not change it.

## Root cause (BUG 1)
`setNearMeChip` is global but calls `applyNearMeClientFilter`, which is private to the IIFE closure. From a global function, `applyNearMeClientFilter` is undefined → `ReferenceError` before any filtering runs.

## Fix
Move `nearMeState` and `setNearMeChip` **inside** the IIFE (next to `applyNearMeClientFilter`), then expose `setNearMeChip` on `window` so the inline `onclick` attributes still resolve it.

### Step 1 — remove the global declarations (lines 524-535)
Delete:
```js
        var nearMeState = { type: null, rating: null, distance: null, search: '' };

        function setNearMeChip(key, value, chipEl) {
            nearMeState[key] = (value === null || value === '') ? null : value;
            // Update chip-active class within the same chip group
            var group = chipEl.closest('.chips-group');
            if (group) {
                group.querySelectorAll('.chip').forEach(function (c) { c.classList.remove('chip-active'); });
            }
            chipEl.classList.add('chip-active');
            applyNearMeClientFilter();
        }
```
(but keep `applyNearMeFilter` at lines 510-522 exactly as-is).

### Step 2 — re-declare inside the IIFE, right after `applyNearMeClientFilter` (after line 631)
Insert:
```js
            var nearMeState = { type: null, rating: null, distance: null, search: '' };

            function setNearMeChip(key, value, chipEl) {
                nearMeState[key] = (value === null || value === '') ? null : value;
                var group = chipEl.closest('.chips-group');
                if (group) {
                    group.querySelectorAll('.chip').forEach(function (c) { c.classList.remove('chip-active'); });
                }
                chipEl.classList.add('chip-active');
                applyNearMeClientFilter();
            }
            window.setNearMeChip = setNearMeChip;
```
`nearMeState` is currently first referenced inside the IIFE at `reapplyFromQuery` (line 655) and used by `cardMatchesState` (line 578) etc. Placing this block after `applyNearMeClientFilter` keeps all references in-scope (they're already inside the IIFE). Note `reapplyFromQuery` (line 648) and `cardMatchesState` (line 571) already run inside the IIFE, so they will pick up the moved `nearMeState` with no change.

### Step 3 — no change to line 625
Leave `_firstDefined(f.attributes, nearMePropMap.id, [])` as-is (already correct/3-arg).

## Scope guard
- Only edit `Views/NearMe/Index.cshtml`.
- Do NOT touch `Views/Explore/Index.cshtml`, `wwwroot/js/maps.js`, controllers, or VMs.
- Do NOT modify `applyNearMeFilter` (lines 510-522).

## Validation
1. Reload NearMe page with DevTools console open.
2. Click each type / rating / distance chip → **zero console errors**.
3. Confirm the sponsor list filters, non-matching markers disappear from the map, and the map zooms to fit the remaining visible markers.
4. Confirm the destinationId dropdown + Go button still do a server round-trip and client filters re-apply from the query string afterwards.
5. (No C# change; `dotnet build` optional but should still pass.)
