# Explore ↔ Trip flow improvements (EGYXPLORE)

## Confirmed decisions
- **Item 4:** Interests (→ `Category`) + Budget (→ `TicketPrice`) are the real destination filters.
  Duration (derived from `Start`/`End`) + Companions are stored on `TripPlan`, **not** filters.
  Schema additions: `TripPlan.Budget` (`decimal?`) + `TripPlan.Companions` (`int?`). Interests are NOT persisted (client filter only).
- **Item 2/3:** One `TripPlan` with `Status = "Draft"` per tourist (get-or-create). "Add to My Trip"
  and the builder read/write that draft. "Save Trip" finalizes it to `Status = "Active"`. A later
  "Add to My Trip" starts a new Draft. Trip Details + reorder/edit-dates/delete AJAX are restricted to
  the owning tourist (403 if not owner).

## Shared category mapping (Explore chips ⇄ Trip Interests)
Kept identical in both places so they stay consistent. Chip/checkbox `data-*` values:
`Temples→Category "Temple"`, `Museums→Category "Museum"`, `Nature→Category "Natural"`,
`Hidden Gems→Status "Pending"`, `Adventure→Category "Archaeological"`.
(Task's suggested `Historical`/`Religious` also map to real `Category` values "Historical"/"Religious"
and will be included as options. Flagged: these extend the earlier Explore chip set — both must use the same attributes.)

---

## Schema / migration (items 2,3,4)
**Files:** `Models/TripPlan.cs`, `Data/TouristContext.cs`, new `Migrations/20260707xxxxxx_TripPlanBudgetCompanions.cs` (+Designer), `Migrations/TouristContextModelSnapshot.cs` (auto-updated).
- `TripPlan`: add `public decimal? Budget { get; set; }` and `public int? Companions { get; set; }`.
- `TouristContext.OnModelCreating`: `modelBuilder.Entity<TripPlan>().Property(t => t.Budget).HasColumnType("decimal(10,2)")` (consistency with `Destination.TicketPrice`).
- Generate migration via `dotnet ef migrations add TripPlanBudgetCompanions`. If it comes out empty
  (known stale-snapshot quirk), manually populate `Up` with `AddColumn`/`AddColumn` for `Budget`
  (`decimal(10,2)`, nullable) and `Companions` (`int`, nullable), `Down` drops them.
- `Status = "Draft"` is just a string value — **no schema change**.

---

## Item 1 — Context-aware "Back" on Destination Details
**Files:** `Controllers/DestinationController.cs`, `Views/Destination/Details.cshtml`.
- `Details(int id)`: set `ViewBag.BackUrl` from `Request.Headers["Referer"]`:
  contains `/Explore` → `"/Explore"`; contains `/Trip` → `"/Trip"`; else `"/Destination"` (admin list).
- `Details.cshtml` Back button: `href="@ViewBag.BackUrl"` with
  `onclick="if(document.referrer){history.back();return false;}"`.
  → Uses browser back when there is a referrer (preserves Explore scroll/filter state for tourists,
  returns admins to the admin list), and falls back to `BackUrl` on a direct load.
- No other admin-CRUD changes.

## Item 2 — "Add to My Trip" on Destination Details
**Files:** `Controllers/TripController.cs`, `Views/Destination/Details.cshtml`, `View Model/TripBuilderVM.cs`, `Repositories/ITripPlanRepository.cs`, `Repositories/TripPlanRepository.cs`.
- `TripBuilderVM`: add `public decimal? Budget { get; set; }` and `public int? Companions { get; set; }` (so the builder form binds/saves them).
- `ITripPlanRepository`/`TripPlanRepository`: add
  `TripPlan? GetDraftTrip(int touristId)` (first `Status=="Draft"` for tourist),
  `void AddStop(TripDestination stop)`, `void RemoveStop(int tripDestinationId)`,
  reuse existing `RemoveTripDestinations(int)` for replace-on-save.
- `TripController`:
  - `GetOrCreateDraftTrip(int touristId)`: return `GetDraftTrip`; if null, create
    `new TripPlan { Title="My Trip", StartDate=Today, EndDate=Today.AddDays(7), Status="Draft", TouristId }`, `Add`+`Save`, return.
  - `[HttpPost][Authorize(Roles="User")] AddToTrip(int id)`: resolve tourist → `draft = GetOrCreateDraftTrip`;
    if `draft.TripDestinations` has no row with `DestinationId==id`, append
    `new TripDestination { DestinationId=id, Visit_Order = (max+1), ArrivalDate=draft.StartDate, DepartureDate=draft.StartDate.AddDays(1) }`; `Save`; `return RedirectToAction("Index")`.
  - `Index`: after resolving tourist, load `draft = GetDraftTrip(tourist.Id)`; if present, pre-populate
    the `TripBuilderVM` `Stops` so the draft's destinations are `Selected=true` with their `ArrivalDate`/`DepartureDate`,
    and set `Title`/`StartDate`/`EndDate`/`Budget`/`Companions` from the draft. Pass `ViewBag.DraftTripId`.
  - `Create` (POST): `draft = GetDraftTrip(tourist.Id)`; if `draft != null` → `RemoveTripDestinations(draft.Id)`,
    set `Title/StartDate/EndDate/Budget/Companions/Status="Active"`, add selected stops
    (`Visit_Order = idx+1`); `Update(draft)`+`Save`. Else create new `TripPlan` `Status="Active"` (existing behavior). `RedirectToAction("Index")`.
- `Details.cshtml`: inside the card footer, when `@User.IsInRole("User")`, render a small POST form
  (`asp-action="AddToTrip" asp-controller="Trip"`) with a hidden `id` and a
  `<button class="btn btn-success">Add to My Trip</button>` (antiforgery). Guests/admins don't see it;
  unauthenticated POST is bounced to login by `[Authorize]`.

## Item 3 — Trip Details page (new)
**Files (new):** `Views/Trip/Details.cshtml`; **changed:** `Controllers/TripController.cs`, `Views/Trip/Index.cshtml` (link trips → Details), `Views/Destination/Details.cshtml` (none), `_Layout`/page (SortableJS CDN).
- `TripController`:
  - `[Authorize(Roles="User")] Details(int id)`: resolve tourist; `trip = GetByIdWithDetails(id)`;
    if `trip==null || trip.TouristId != tourist.Id` → `Forbid()`/`NotFound()`; `return View(trip)`.
  - `[HttpPost][ValidateAntiForgeryToken] ReorderStops(int id, [FromBody] List<int> orderedStopIds)`:
    verify ownership; for each `stopId` at index `i`, set `Visit_Order = i+1` (via `_context.TripDestinations`); `Save`; `return Json(...)`.
  - `[HttpPost][ValidateAntiForgeryToken] UpdateStopDates(int stopId, DateTime arrival, DateTime departure)`:
    verify ownership via stop→trip; update; `Save`.
  - `[HttpPost][ValidateAntiForgeryToken] DeleteStop(int stopId)`: verify ownership; `RemoveStop(stopId)`; `Save`.
- `Views/Trip/Details.cshtml`:
  - Header: title, `Start–End` dates, status badge, "Back to Trip" link (`/Trip`).
  - `#tripDetailsMap` placeholder (sized like Explore's) with each destination entry carrying
    `data-lat`/`data-lng`/`data-id` for a future map.
  - Ordered `<ul id="stopList">` of `<li data-stop-id="...">` stops: name, city, arrival/departure,
    an "Edit dates" toggle revealing an inline date-edit row (two date inputs + Save → `UpdateStopDates`),
    and a "Remove" button (JS `confirm`) → `DeleteStop`.
  - SortableJS (`https://cdnjs.cloudflare.com/ajax/libs/Sortable/1.15.6/Sortable.min.js`): on `onEnd`,
    collect `data-stop-id` order → AJAX `ReorderStops`. After drop, show a small inline notice nudging
    the tourist to review/adjust that stop's dates (reorder can make stale dates nonsensical).
  - `@Html.AntiForgeryToken()`; AJAX sends `RequestVerificationToken` header.
- `Views/Trip/Index.cshtml`: each trip in "My Trips" links to `/Trip/Details/@trip.Id`.

## Item 4 — Trip builder: map + planning filters
**Files:** `Views/Trip/Index.cshtml`, `Controllers/TripController.cs` (no new action; filter is client-side).
- Add `#tripBuilderMap` placeholder in the right ("Your Itinerary") column header (styled like Explore's;
  flagged: the builder had **no** map placeholder before — added here per item 4's intent).
- Add a `.filter-bar`-style panel (above the builder columns) with:
  - **Duration**: read-only "X days" derived from the existing Start/End date inputs (no new field).
  - **Budget**: numeric input (binds to `TripBuilderVM.Budget`, saved on Save Trip).
  - **Companions**: numeric input (binds to `TripBuilderVM.Companions`, saved on Save Trip).
  - **Interests**: checkboxes using the shared mapping (Temples/Temple, Museums/Museum, Nature/Natural,
    Hidden Gems/Pending, Adventure/Archaeological, + Historical/Religious) with `data-category`/`data-status`.
  - **Filter** button (client-side JS) that shows/highlights picker rows matching selected Interests
    (`Category` or `Status=="Pending"`) and `TicketPrice <= Budget`. Duration/Companions do NOT filter.
- The `Budget`/`Companions` inputs live inside the existing `tripForm` so `Create` persists them onto the draft.

---

## Assumptions flagged
- Duration is derived from `Start`/`End` (no separate column). Companions/Budget stored on the draft.
- Interests are a client-side filter only (not persisted), per approved decision.
- Explore chips and Trip Interests use one identical `data-*` mapping (documented above); both must stay in sync.
- Trip builder previously had no map placeholder; `#tripBuilderMap` is new.
- "Save Trip" transitions the draft → `Active`; a later "Add to My Trip" starts a fresh Draft.
- Back button prefers `history.back()` (preserves Explore scroll/filter) and falls back to a referrer-aware URL.

## Testing (end-to-end)
1. `dotnet build` → 0 errors. Apply migration (`dotnet ef database update`); verify `Budget`/`Companions` columns exist.
2. **Integration harness** (throwaway console, calls real repos against the DB) asserts:
   - AddToTrip: tourist has a `Status="Draft"` `TripPlan` with one `TripDestination` for the picked id.
   - ReorderStops: `Visit_Order` values persist in new order after drop.
   - UpdateStopDates: arrival/departure persist.
   - DeleteStop: `TripDestination` row removed while `Destination` remains.
   - Create/Save: draft → `Status="Active"`.
   (Clean up inserted test rows afterwards.)
3. **Smoke test** (app running): 
   - Explore → Details → Back returns to Explore with filters intact (history.back); admin Destination/Index → Details → Back returns to admin list.
   - As tourist: Details "Add to My Trip" → lands on `/Trip` with the destination pre-checked in the builder.
   - `/Trip/Details/{id}` renders with map placeholder + sortable list; AJAX reorder/edit/delete respond 200 and persist.
