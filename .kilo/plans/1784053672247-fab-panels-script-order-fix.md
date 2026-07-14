# Fix: AI assistant & Sponsor notification panels don't open

## Actual root cause (verified by static analysis)
The two toggle `<script>` blocks in `Views/Shared/_Layout.cshtml` execute **during initial HTML parsing, before the FAB buttons and panel containers they target are parsed**, so `getElementById(...)` returns `null` and no `click` listener is ever bound.

- AI toggle script: ~lines 553–588 → `getElementById('aiAssistantBtn')`, `getElementById('aiAssistantPanel')`.
- Notification toggle script: ~lines 590–737 → `getElementById('notificationFabBtn')`, `getElementById('notificationPanel')`.
- But the targets are declared **later** in the same file:
  - AI button `id="aiAssistantBtn"` ~line 831
  - AI panel `id="aiAssistantPanel"` ~line 842
  - NotificationFab button renders `id="notificationFabBtn"` ~line 840 (`@await Component.InvokeAsync("NotificationFab")`)
  - Notification panel `id="notificationPanel"` / `notificationPanelBody` ~lines 870 / 884

Inline scripts run synchronously at parse time, so the elements don't exist yet → `btn`/`panel` are `null` → the `if (btn && panel)` guards silently skip `addEventListener`. Result: clicking either FAB does nothing. Because **both** panels share this pattern and live in the same layout, they broke together — exactly the reported symptom. This is independent of Leaflet and of z-index: the handler is simply never attached.

## Hypotheses checked and ruled out
- **Leaflet z-index (visual stacking):** Not the cause. FABs + both panels already use `z-index: 1040` (`.fab-base` ~908, `.ai-widget-panel` ~943, `.notification-widget-panel` ~1063). Leaflet's highest z-index is `.leaflet-top`/`.leaflet-bottom` = **1000** (leaflet.css 1.9.4). 1040 > 1000, so panels already paint above the map. Bumping z-index alone would NOT open the panels (handler still unbound). We still apply the bump as defensive hardening (requested).
- **`maps.js` global click listener intercepting/closing panels:** Not present. `wwwroot/js/maps.js` only adds `map.on('click', …)` (scoped to the map element) and `window` resize listeners. No `document`-level listener, no "click outside to close" logic.
- **Leaflet control capturing the click:** Not applicable — the handler is never bound, so the failure happens before any click reaches the button. After the fix, DevTools → Event Listeners should show the `click` listener firing on the FAB.

## Fixes

### Fix 1 (primary) — bind handlers after the DOM exists
In `Views/Shared/_Layout.cshtml`, defer both IIFEs so they run after parse. Minimal, low-risk change: wrap each IIFE body in `DOMContentLoaded`.

- AI toggle script (~line 555): `(function () {` → `document.addEventListener('DOMContentLoaded', function () {`, and closing `})();` → `});`.
- Notification toggle script (~line 592): same wrapping.

(Equally valid alternative: move both `<script>` blocks to the end of `<body>`, after the FAB/panel markup and after `@await RenderSectionAsync("Scripts")`. The `DOMContentLoaded` wrap is the smaller diff.)

After this, `getElementById` resolves the elements, `addEventListener('click', …)` binds, and both panels open.

### Fix 2 (defensive hardening, as requested) — explicit high z-index via shared token
Add one shared token and apply to the FAB + both panels so they are unambiguously above Leaflet and any future overlay (the "shared CSS, not a one-off patch" requirement).

- `wwwroot/css/site.css` `:root` (after `--nav-offset`): add `--egy-fab-zindex: 9999;`
- Inline `<style>` of `_Layout.cshtml`:
  - `.fab-base` (~908): `z-index: var(--egy-fab-zindex);` (replace `1040`)
  - `.ai-widget-panel` (~943): `z-index: var(--egy-fab-zindex);`
  - `.notification-widget-panel` (~1063): `z-index: var(--egy-fab-zindex);`

## Validation
1. `dotnet build` → 0 errors.
2. DevTools → Elements → select `#aiAssistantBtn` → Event Listeners pane → confirm a `click` listener is now present (absent before the fix).
3. Non-map page `/` (Home): click AI FAB → panel opens; close works.
4. Map page `/Explore`: click AI FAB → panel opens above the Leaflet map, no overlap.
5. Signed in as **Sponsor**:
   - Non-map sponsor page (e.g. `/SponsorPortal`): click notification bell → panel opens, loads notifications; mark-all-read + delete work.
   - Any sponsor map page: same — panel opens above the map.
6. Non-sponsor (Tourist / Admin / guest): notification FAB shows with no badge; opening shows empty state "You're all caught up 🏺"; AI FAB still opens.
7. Regression: Leaflet maps on `/Explore` and `/NearMe` still pan/zoom and show popups normally.

## Risks / notes
- `DOMContentLoaded` fires after the full document (including each page's `@section Scripts`), so no ordering regression with page scripts.
- z-index `9999` places the FAB panels above Bootstrap modals/toasts (modal = 1055). Acceptable for always-on global widgets; if FAB-below-modal is ever wanted, use a value between 1000 and 1055 (e.g. 1100).

## Open question
- None blocking. Confirm whether FAB panels should sit above Bootstrap modals (9999) or only above Leaflet (≈1100).
