# Plan: Fix ArcGIS API key timing bug + view.resize TypeError + missing theme stylesheet

## Bug A — API key timing (sign-in dialog)

### Root cause

`window.esriConfig` is never declared before the ArcGIS CDN loads. `_ensureApiKey()` in `maps.js` tries to set `window.esriConfig.apiKey` *after* the SDK has already initialized and after layer requests have started — too late per Esri's docs. The guard `if (window.esriConfig && ...)` is always false, so no key is ever attached and the SDK falls back to an interactive sign-in prompt.

### Changes

#### 1. `Views/Shared/_Layout.cshtml`

- Add `@inject IConfiguration Configuration` near the top (project already uses `@inject` in `_ViewImports.cshtml` for `IStringLocalizer`, so this is consistent).
- Before the existing `<script type="module" src="https://js.arcgis.com/5.1/"></script>` (line 20), insert:

```html
<script>
    var esriConfig = { apiKey: "@Configuration["ArcGIS:ApiKey"]" };
</script>
```

Order matters: the plain `<script>` must come before the module script so the SDK reads `esriConfig` during initialization. The current key is alphanumeric plus `.`/`_`/`-` — safe to embed directly in a JS string; Razor HTML-encodes output automatically.

#### 2. `wwwroot/js/maps.js`

- In `_ensureApiKey()`, remove the runtime assignment. Keep the function as a no-op (or remove it entirely if no longer called anywhere — verify call sites first).
- `_ensureConfig()` stays as-is; it still fetches `/Map/GetMapConfig` for `destinationsLayerUrl` / `branchesLayerUrl`.

#### 3. `Controllers/MapController.cs`

- Optionally remove `apiKey` from `GetMapConfig` response since nothing else consumes it client-side. Leave it if you prefer a minimal-change approach — it's harmless either way.

---

## Bug B — `TypeError: view.resize is not a function`

### Root cause

`maps.js` contains three occurrences of:

```js
view.watch('stationary', function () {
    view.resize();
});
```

This was carried over from the old Leaflet code's `map.invalidateSize()` resize-handling pattern, but `MapView` in the ArcGIS Maps SDK has **no `resize()` method**. `MapView` automatically handles container size changes on its own via an internal resize observer; there is no manual step needed. Calling `view.resize()` throws immediately, crashing every map.

### Fix

Delete all three occurrences of the `view.watch('stationary', function () { view.resize(); });` block entirely. Do not replace it with anything — `MapView` handles resizing on its own.

Also check for any other `.watch(` calls on a `view` or layer object. There are no remaining load-bearing `.watch()` calls in `maps.js` (only these three resize blocks), so no migration to `reactiveUtils.watch()` is needed.

---

## Bug C — Map overflow/gaps and mispositioned popups (missing theme stylesheet)

### Root cause

`Views/Shared/_Layout.cshtml` only includes the ArcGIS JS module script:

```html
<script type="module" src="https://js.arcgis.com/5.1/"></script>
```

It is missing the required theme CSS:

```html
<link rel="stylesheet" href="https://js.arcgis.com/5.1/esri/themes/light/main.css" />
<script type="module" src="https://js.arcgis.com/5.1/"></script>
```

Per Esri's docs, automatic CSS inclusion only applies when using `<arcgis-map>` web components. This codebase uses the classic `MapView`/`Map` core classes directly (via `$arcgis.import`), which explicitly requires the manual stylesheet link. Without it:
- `.esri-view-root`/`.esri-view-surface` don't get `width:100%; height:100%; position:...` rules → the view doesn't fill its container (overflow/blank-stripe gaps)
- Popup has no docking styles → renders as an unstyled block in normal document flow below the map instead of docked over it

### Fix

#### 1. `Views/Shared/_Layout.cshtml`

Add the theme stylesheet link, matching the exact same SDK version as the script tag:

```html
<link rel="stylesheet" href="https://js.arcgis.com/5.1/esri/themes/light/main.css" />
<script type="module" src="https://js.arcgis.com/5.1/"></script>
```

Use the `light` theme to match the app's existing warm/light Egyptian-tourism aesthetic (site.css palette: `--egy-light: #FDF9F3`, `--egy-dark: #1E120A` — light background, so `light` theme is correct). Keep the version number identical between the `<link>` and `<script>` tags.

#### 2. `wwwroot/js/maps.js` — clear placeholder content defensively

The map container `<div>`s in all views contain a hardcoded `.map-watermark` placeholder child ("Map will render here" icon/text). Confirm whether `MapView`'s constructor clears existing child content automatically — if it does **not**, add an explicit step at the start of both `initWfsMap` and `initLocationPicker` to clear the container's contents right before constructing the `MapView`:

```js
document.getElementById(mapElId).innerHTML = '';
```

This is a defensive fix on top of the CSS fix, not a replacement for it.

#### 3. Sanity-check the map panel CSS isn't fighting the fix

Map panel containers (e.g. `.explore-map-container`, `.nearme-map-container`, `.trip-builder-map`) use `display: flex; align-items: center; justify-content: center;`. Once the theme CSS is in place, the view's root element should get `width: 100%; height: 100%` from Esri's stylesheet and fill correctly regardless of flex centering. If any gap/overflow persists after steps 1–2, check whether the injected `.esri-view-root` element is picking up `width:100%;height:100%` in dev tools computed styles, and if the site's own CSS is overriding it. Don't preemptively rewrite this CSS — verify first.

---

## Verification

1. Load `/Explore`, `/Trip`, `/Trip/Details/{id}`, `/NearMe`, `/Destination/Create`, `/SponsorBranch/Create`.
2. Confirm no `TypeError: view.resize is not a function` in console.
3. Confirm the map fills its entire container — no striped placeholder background visible around/behind it, no blank gaps on any edge.
4. Click a marker; confirm the popup docks properly within/over the map area instead of rendering as a full-width block below the map.
5. Confirm Network tab shows FeatureServer requests carrying the API key automatically; no sign-in dialog.
6. Resize the browser window and confirm the map and popup both adapt correctly.
7. If the dialog is gone but layers are empty/403, the API key needs item-access grants in ArcGIS Online for the Destinations/Branches layers (out-of-scope code change; user action in AGOL).
