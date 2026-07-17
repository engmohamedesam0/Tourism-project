# Fix ArcGIS map canvas rendering invisible — `align-items: center` collapse

## Summary
ArcGIS injects a `.esri-view-surface` div inside map containers that sizes via percentage-height cascading (`height: 100%` at each level). When the parent container has `display: flex; align-items: center`, the flex item (`.esri-view-root`) no longer stretches to the container's full cross-axis size — it collapses toward zero because it has no intrinsic height. ArcGIS's internal `overflow: hidden` on `.esri-view-surface` then clips the correctly-sized `<canvas>` (one level deeper) to nothing.

## Affected files

| File | Container selector | Has watermark | Has map init |
|------|-------------------|---------------|--------------|
| `Views/NearMe/Index.cshtml` | `.nearme-map-container` | ✅ `.map-watermark` | ✅ `initWfsMap` |
| `Views/Explore/Index.cshtml` | `.explore-map-container` | ✅ `.map-watermark` | ✅ `initWfsMap` |
| `Views/Trip/Index.cshtml` | `.trip-builder-map` | ✅ `.map-watermark` | ✅ `initWfsMap` |
| `Views/Trip/Details.cshtml` | `.trip-details-map` | ✅ `.map-watermark` | ✅ `initWfsMap` |
| `Views/NearMe/Details.cshtml` | `.sponsor-map-container` | ✅ `.map-watermark` | ❌ no init call |
| `Views/Destination/Create.cshtml` | `#destinationMap` (inline style) | ❌ | ✅ `initLocationPicker` |
| `Views/Destination/Edit.cshtml` | `#destinationMap` (inline style) | ❌ | ✅ `initLocationPicker` |
| `Views/SponsorBranch/Create.cshtml` | `#branchMap` (inline style) | ❌ | ✅ `initLocationPicker` |
| `Views/SponsorBranch/Edit.cshtml` | `#branchMap` (inline style) | ❌ | ✅ `initLocationPicker` |

**Files needing CSS fix (flex + centering):**
- `NearMe/Index.cshtml` — `.nearme-map-container`
- `Explore/Index.cshtml` — `.explore-map-container`
- `Trip/Index.cshtml` — `.trip-builder-map`
- `Trip/Details.cshtml` — `.trip-details-map`
- `NearMe/Details.cshtml` — `.sponsor-map-container`

**Files NOT needing the flex fix** (use inline styles, no flex centering):
- `Destination/Create.cshtml`, `Destination/Edit.cshtml`, `SponsorBranch/Create.cshtml`, `SponsorBranch/Edit.cshtml`

**Note:** `NearMe/Details.cshtml` has the container CSS and watermark markup but contains no JS to actually initialize the ArcGIS map. That is a separate issue — out of scope for this fix, but worth flagging.

## Changes required

### 1. Fix map container CSS in each affected view

For each of the 5 affected `.cshtml` files, edit the map-container rule:

**Before** (example from NearMe/Index.cshtml lines 465-472):
```css
.nearme-map-container {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: repeating-linear-gradient(45deg, #e7e0d5 0 18px, #efe9df 18px 36px);
}
```

**After:**
```css
.nearme-map-container {
    position: absolute;
    inset: 0;
    background: repeating-linear-gradient(45deg, #e7e0d5 0 18px, #efe9df 18px 36px);
}
```

Then update the `.map-watermark` rule from:

```css
.map-watermark {
    text-align: center;
    color: var(--egy-muted-gold);
    opacity: 0.65;
}
```

to:

```css
.map-watermark {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    text-align: center;
    color: var(--egy-muted-gold);
    opacity: 0.65;
}
```

Apply the same transformation to each file's equivalent selectors:
- `Explore/Index.cshtml`: `.explore-map-container` + `.map-watermark`
- `Trip/Index.cshtml`: `.trip-builder-map` + `.map-watermark`
- `Trip/Details.cshtml`: `.trip-details-map` + `.map-watermark`
- `NearMe/Details.cshtml`: `.sponsor-map-container` + `.map-watermark`

**Responsive blocks** — each file also has a `@@media (max-width: 991.98px)` block that redefines the container. Ensure the `display: flex`, `align-items: center`, `justify-content: center` are removed from the responsive `.map-container` rule too. The `.map-watermark` absolute positioning already works in the responsive context because the container is `position: relative` there.

### 2. Add defensive ArcGIS overrides in `wwwroot/css/site.css`

Append at the end of `site.css`:

```css
/* ArcGIS view must always fill its container regardless of parent flex context */
.esri-view-root,
.esri-view-surface {
    width: 100% !important;
    height: 100% !important;
}
```

This is a safety net, not a replacement for the root-cause fix above.

### 3. Confirm placeholder clearing in `wwwroot/js/maps.js`

Both `initWfsMap` and `initLocationPicker` already contain `mapEl.innerHTML = '';` immediately before `new MapView(...)`:

- `initWfsMap`: line 283 — `mapEl.innerHTML = '';` → line 285 `view = new MapView(...)`
- `initLocationPicker`: line 334 — `mapEl.innerHTML = '';` → line 336 `view = new MapView(...)`

No change needed; just verify this remains intact.

## Verification plan

1. Reload `/NearMe`, `/Explore`, `/Trip`, `/Trip/Details/{id}` and confirm maps render.
2. In DevTools Elements panel, hover `.esri-view-surface` on each page — rendered size should match the container (not `× 0`).
3. Confirm basemap tiles and markers are visible.
4. Check `NearMe/Details` — same fix applies even though it currently has no JS map init.
5. Destination and SponsorBranch create/edit pages do not need CSS changes (no flex centering), but verify maps still render there after the global `site.css` override lands.

---

# Fix duplicate markers per point + filter-doesn't-affect-map symptom

## Summary
`initWfsMap` in `wwwroot/js/maps.js` adds the raw ArcGIS `FeatureLayer` to the map (`map.add(sourceLayer)`) and separately builds custom-styled overlay graphics into `overlayGraphicsLayer`. Both layers render visible markers for the same features, producing duplicate markers. The filter chips (`filterMarkers()`) only toggle visibility on the overlay graphics, so the unfiltered raw `FeatureLayer` markers remain visible — making the filter appear broken.

## Root cause confirmed
In `maps.js` `initWfsMap` (around line 216):
```js
sourceLayer = new FeatureLayer({ url: layerUrl, ... });
map.add(sourceLayer);           // renders default symbology, unfiltered
// ...
var result = await sourceLayer.queryFeatures(query);
result.features.forEach(function (f) {
    var graphic = new Graphic({ ... });  // custom styled marker
    overlayGraphicsLayer.add(graphic);   // second set of markers
});
overlayGraphicsLayer is also added to the map → two markers per feature.
```

`filterMarkers()` (line 108-118) only sets `graphic.visible = show` on overlay graphics. `sourceLayer` markers are untouched.

## Fix

### 1. Hide the raw `FeatureLayer` in `initWfsMap`

In `wwwroot/js/maps.js`, immediately after `map.add(sourceLayer);` (line 216), add:

```js
sourceLayer.visible = false;
```

This keeps `sourceLayer` in the map's layer collection (so `queryFeatures`, `whenLayerView`, and `layer()` accessor all continue to work) but prevents its default-rendered markers from appearing on screen.

**Do NOT** remove `map.add(sourceLayer)` — some ArcGIS query/layer-view operations behave more reliably when the layer is part of the map collection even if invisible.

### 2. Check `initLocationPicker` for the same pattern

In `initLocationPicker`, `contextLayer` is created at line 407:
```js
var ctxLayer = new FeatureLayer({ url: ctxLayerUrl, outFields: ['*'] });
ctxLayer.queryFeatures({ ... })
```

`ctxLayer` is **never** added to the map (`map.add(ctxLayer)` is absent). It is only used for `queryFeatures`, and the resulting graphics are added to `overlayLayer`. Therefore there is **no duplicate-rendering issue** in `initLocationPicker` — no change needed there.

### 3. No other files need changes

This is a single-line fix in one JS file. No CSS, no view changes.

## Verification

1. Reload `/Explore`, `/NearMe`, `/Trip`, `/Trip/Details/{id}`.
2. Confirm exactly **one** marker per feature (not two).
3. On `/Explore`, click category chips (Temples, Museums, Nature, Adventure, All) — map markers should visibly filter to match.
4. On `/NearMe`, confirm TYPE/RATING/DISTANCE page-reload filters still show only matching markers.
5. Confirm popups still open on marker click (they're bound to overlay graphics, unaffected).
6. Check `Destination/Create`, `Destination/Edit`, `SponsorBranch/Create`, `SponsorBranch/Edit` — no visual regression; `initLocationPicker` is unaffected.
