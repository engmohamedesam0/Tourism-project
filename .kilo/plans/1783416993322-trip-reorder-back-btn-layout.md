# Trip Details reorder fix, Back-button restyle, Trip page layout

Source of truth: current code in `Tourist_Project_MVC/`. Three independent fixes; no new functionality.

## Item 1 — Drag-to-reorder on Trip Details doesn't work

### Root causes (two)
- **RC1 (nothing initializes):** `Views/Trip/Details.cshtml` (the `@section Scripts` block, ~lines 190-274) uses jQuery (`$`) but **jQuery is not loaded on this page**. `grep jquery` shows it is only included by `Views/Trip/Index.cshtml` (line 363, CDN `jquery@3.7.1`) and `_ValidationScriptsPartial`. So the IIFE throws `ReferenceError: $ is not defined` at the very first line (`var token = $('input[name=__RequestVerificationToken]').val();`), which aborts the whole script → `new Sortable(...)` never runs → dragging does nothing.
- **RC2 (save silently fails):** `TripController.ReorderStops(int id, [FromBody] List<int> orderedStopIds)` binds `id` from the route/query, but the AJAX posts `id` **inside** the JSON body (`data: JSON.stringify({ id: @Model.Id, orderedStopIds: orderedIds })`). The body is consumed by `orderedStopIds`, so `id` is `0` → `GetByIdWithDetails(0)` is null → `Forbid()` (403). The drop *looks* like it worked (badges renumber in `success`) but the order is never persisted; a page reload shows the old order.

### Fix
Files: `Views/Trip/Details.cshtml` only (controller signature stays unchanged).

1. **RC1:** Add jQuery to the Details scripts block, immediately before the SortableJS include (same version used on Index):
   `<script src="https://cdn.jsdelivr.net/npm/jquery@3.7.1/dist/jquery.min.js"></script>`
   (Alternative, if the implementer prefers to drop the dependency: rewrite the inline script in vanilla JS — `document.getElementById`, `fetch` with `RequestVerificationToken` header, `li.dataset.stopId`. Either approach fixes RC1; jQuery-include is lower-risk and matches the Index page.)
2. **RC2:** Change the reorder AJAX so `id` travels via the **route**, and the body is the bare `orderedStopIds` array:
   - `url: '@Url.Action("ReorderStops", "Trip", new { id = Model.Id })'`  → emits `/Trip/ReorderStops/{id}`.
   - `data: JSON.stringify(orderedStopIds)` (bare array binds to `[FromBody] List<int>`), keep `contentType: 'application/json'` and the `RequestVerificationToken` header.
   - Remove `id` from the posted object.
3. Keep `getStopIds()` reading `data-stop-id` (already present on each `<li>`); keep the `success` badge-renumber; keep `onEnd`, `ghostClass`, `handle: '.stop-handle'`. No structural change to the list or controller.

### Verification
- As the owning tourist, open `/Trip/Details/{id}`, drag a stop, drop → Network tab shows `POST /Trip/ReorderStops/{id}` **200** (not 403). Reload → new order persists (round-trip correct).

## Item 2 — Restyle the "Back" button (styling only)

### Decision
Add **one shared class `.btn-back`** to `wwwroot/css/site.css` using the existing design-system variables (`--egy-primary` gold, `--egy-light` tint, `--egy-dark`), modeled on the existing `.btn-group .btn-outline-secondary:hover` semantics (gold border + gold text, light hover tint, slight lift). Use a pill radius (`border-radius: 50rem`) per the suggested clean pattern. Keep behavior identical.

### `.btn-back` spec (site.css)
- `display:inline-flex; align-items:center; gap:.4rem;`
- `border:1px solid var(--egy-primary); color:var(--egy-primary); background:#fff;`
- `border-radius:50rem; padding:.35rem .9rem; font-weight:600;` hover: `background:var(--egy-light); border-color:var(--egy-accent); color:var(--egy-dark); transform:translateY(-1px);` + focus-visible outline in `--egy-primary`.

### Files / changes
1. `wwwroot/css/site.css` — add `.btn-back` (global, so every page reuses it).
2. Replace `class="btn btn-secondary"` / `btn btn-outline-secondary` with `class="btn-back"` on every Back link found:
   - `Views/Destination/Details.cshtml` (line ~106) — **keep** `href="@backUrl"` and `onclick="if(document.referrer){history.back();return false;}"` (context-aware behavior unchanged) + keep `<i class="bi bi-arrow-left me-1"></i> Back`.
   - `Views/Trip/Details.cshtml` (line ~14, "Back to Trip").
   - `Views/Sponsor/Details.cshtml` (line ~103), `Views/Sponsor/Edit.cshtml` (line ~52).
   - `Views/TripPlan/Details.cshtml` (line ~104), `Views/Tourist/Details.cshtml` (line ~196).
   - `Views/Reward/Details.cshtml` (line ~74), `Views/Mission/Details.cshtml` (line ~82).
   Labels/icons stay as-is; only the class changes. No controller/logic changes.

## Item 3 — Map + filters as one full-width section on the Trip builder

### Current state
In `Views/Trip/Index.cshtml` the **entire page** is wrapped in `<div class="container mt-4" style="max-width:1200px;">`. The `.filter-bar` sits *above* the `.row g-4` (picker + itinerary), and `#tripBuilderMap` lives inside the itinerary `col-lg-7`. Note: `_Layout` already wraps `@RenderBody()` in `<main class="inner-page-wrapper"><div class="container">…</div></main>`, so any child is capped by that container — a true full-bleed needs a break-out.

### Fix
Files: `Views/Trip/Index.cshtml` (markup restructure) + `wwwroot/css/site.css` (or the view's `@section Scripts` style block).

1. Lift `#tripForm` out of the `max-width:1200px` container so it is a full-width sibling (header "Plan Your Trip" + "My Trips" timeline stay in the constrained container above the form).
2. Inside `#tripForm` (which must still wrap **both** the filter inputs and the picker/itinerary so "Save Trip" posts Budget/Companions):
   - **Full-bleed section** (map + filters together):
     `<section class="trip-map-filter-section">` with inner `<div class="container-fluid px-3 px-lg-4">` → `<div class="row align-items-stretch">` → `<div class="col-lg-8">#tripBuilderMap</div>` + `<div class="col-lg-4">.filter-bar (Duration/Budget/Companions/Interests + Filter)</div>`.
   - **Contained builder** (unchanged behavior): `<div class="container mt-4" style="max-width:1200px;">` → existing `<div class="row g-4">` picker + itinerary.
3. Move the existing `.filter-bar` markup into the `col-lg-4`, and the existing `#tripBuilderMap` markup into the `col-lg-8`. **No JS changes required** — `#tripInterests`, `#Budget`, `#tripFilterBtn`, `#StartDate`, `#EndDate`, `.picker-row` all remain inside the form with the same IDs, so `applyTripFilter()` / duration calc keep working.
4. CSS:
   - `.trip-map-filter-section { width:100vw; position:relative; left:50%; right:50%; margin-left:-50vw; margin-right:-50vw; }` (canonical full-bleed break-out of the centered `_Layout` container). Optional: `body { overflow-x:hidden; }` only if a horizontal scrollbar appears.
   - Give `#tripBuilderMap` a sensible fixed/min height, e.g. `min-height:340px; height:100%;` (was 180px) so the layout doesn't collapse; `align-items-stretch` makes map + filter equal height on desktop.
   - `.filter-bar` keeps its flex-wrap, so inside ~33% width it stacks its fields readably.
5. **Responsive:** Bootstrap `col-lg-*` auto-stacks below `lg`. Chosen stack order on mobile: **map on top, filter panel below** (visual-first). (If reviewer prefers filters-first, swap the two columns — noted as the only open styling choice.)

### Verification
- Desktop: map (~66%) + filter panel (~33%) side-by-side, full window width; picker + itinerary below in the normal centered width.
- Narrow/mobile: stacks (map then filter); map keeps ~340px height; no horizontal scroll; "Filter" still highlights matching picker rows; "Save Trip" still persists Budget/Companions.

## Affected files (summary)
- `Views/Trip/Details.cshtml` — add jQuery include; fix reorder AJAX (id via route, bare array body).
- `Views/Trip/Index.cshtml` — restructure form: full-bleed map+filter section + contained builder; move `#tripBuilderMap` and `.filter-bar`.
- `wwwroot/css/site.css` — add `.btn-back`; add `.trip-map-filter-section` full-bleed + map min-height.
- `Views/Destination/Details.cshtml`, `Views/Trip/Details.cshtml`, `Views/Sponsor/Details.cshtml`, `Views/Sponsor/Edit.cshtml`, `Views/TripPlan/Details.cshtml`, `Views/Tourist/Details.cshtml`, `Views/Reward/Details.cshtml`, `Views/Mission/Details.cshtml` — swap Back `class` to `btn-back` (behavior unchanged).

## Validation (all three)
1. `dotnet build` → 0 errors.
2. Run app; as owning tourist: reorder on `/Trip/Details/{id}` persists (RC2 → 200, reload shows new order).
3. Back buttons on Destination/Sponsor/Trip/etc. render as gold pill with hover tint; Destination Details still returns to Explore vs admin list via `history.back()`/referrer.
4. Trip builder: map+filters span full width side-by-side on desktop, stack on mobile; filter + save still work.

## Open questions / decisions
- Item 1 RC1: recommended fix is adding the jQuery CDN include (matches Index, lowest risk). Vanilla-JS rewrite is the alternative if removing the jQuery dependency is preferred.
- Item 3 mobile stack order: recommended map-on-top; swap columns if filters-first is desired.
