# Plan: Batch Add Destinations to Trip Details Modal

## Goal
Add a checkbox-based batch selection flow to the `#addStopModal` in `Views/Trip/Details.cshtml` that lets tourists check multiple destinations and add them all at once, alongside the existing per-row single-click "Add" button. Both flows must coexist without removing or changing the existing single-click behavior.

---

## Contradiction Found
The user's requirement states: *"the existing success handler already removes/hides the row today."*

**Actual code behavior** (`Details.cshtml` lines 586-641): The single-add success handler only hides the modal and prepends a new stop card to `#stopList`. It does **not** remove or hide the `.list-group-item` row from `#availableDestinationsList`.

**Resolution**: Per the constraint *"The existing single-row `.btn-add-stop` click handler and its AJAX call to AddStop must remain completely untouched in behavior"*, we keep single-add behavior byte-for-byte identical. Row removal applies **only** to the new batch flow. This means a destination single-added while checked will keep its checkbox checked — a minor UX edge case we accept to preserve the constraint.

---

## Files to Change

### 1. `Controllers/TripController.cs`
**Add new action** `AddStops` right after `AddStop` (around line 388). Do NOT modify `AddStop`.

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult AddStops(int id, List<int> destinationIds)
{
    var tourist = ResolveTourist();
    var trip = _tripPlanRepo.GetByIdWithDetails(id);
    if (trip == null || trip.TouristId != tourist.Id)
        return Forbid();

    if (destinationIds == null || !destinationIds.Any())
        return BadRequest();

    var distinctIds = destinationIds.Distinct().ToList();
    var alreadyExists = new List<int>();
    var added = new List<object>();

    var maxOrder = trip.TripDestinations.Any()
        ? trip.TripDestinations.Max(td => td.Visit_Order)
        : 0;

    foreach (var destId in distinctIds)
    {
        if (trip.TripDestinations.Any(td => td.DestinationId == destId))
        {
            alreadyExists.Add(destId);
            continue;
        }

        maxOrder++;
        var newStop = new TripDestination
        {
            TripPlanId = id,
            DestinationId = destId,
            Visit_Order = maxOrder,
            ArrivalDate = trip.StartDate,
            DepartureDate = trip.StartDate.AddDays(1)
        };
        _tripPlanRepo.AddStop(newStop);

        // Defer response building until after Save() so Id is populated
        var dest = _context.Destinations.Find(destId);
        added.Add(new
        {
            stopId = newStop.Id, // populated after Save()
            destinationId = destId,
            destinationName = dest != null ? dest.Name : "",
            destinationCity = dest != null ? dest.City : "",
            lat = dest != null && dest.Location != null ? dest.Location.Y : 0,
            lng = dest != null && dest.Location != null ? dest.Location.X : 0,
            order = newStop.Visit_Order,
            arrivalDate = newStop.ArrivalDate.ToString("MMM dd"),
            departureDate = newStop.DepartureDate.ToString("MMM dd"),
            arrivalDateInput = newStop.ArrivalDate.ToString("yyyy-MM-dd"),
            departureDateInput = newStop.DepartureDate.ToString("yyyy-MM-dd")
        });
    }

    _tripPlanRepo.Save();

    return Json(new { added = added, alreadyExists = alreadyExists });
}
```

**Key decisions**:
- `Distinct()` on input to prevent duplicates within the same batch.
- `alreadyExists` skips destinations already linked to the trip (defensive against race conditions, since the list is server-scoped on initial render).
- Single `_tripPlanRepo.Save()` after the loop, matching `AddStop`'s pattern.
- Response field names are **identical** to `AddStop`'s success JSON so front-end reuse is trivial.

---

### 2. `Views/Trip/Details.cshtml`

#### A. Add checkbox to each row
Inside the `#availableDestinationsList` foreach loop, modify each `.list-group-item`:

**Before** (current):
```html
<div class="list-group-item d-flex align-items-center justify-content-between py-3" ...>
    <div>
        <h6>...</h6>
        <small>...</small>
        ...
    </div>
    <button class="btn-add-stop ...">...</button>
</div>
```

**After**:
```html
<div class="list-group-item d-flex align-items-center justify-content-between py-3" ...>
    <div class="d-flex align-items-center gap-3 flex-grow-1">
        <input type="checkbox" class="form-check-input dest-select-checkbox" data-dest-id="@dest.Id">
        <div class="flex-grow-1">
            <h6 class="mb-1 fw-bold text-dark">@dest.Name</h6>
            <small class="text-secondary"><i class="bi bi-geo-alt me-1 text-gold"></i>@dest.City</small>
            @if (!string.IsNullOrEmpty(dest.Category))
            {
                <span class="badge bg-secondary ms-2">@dest.Category</span>
            }
        </div>
    </div>
    <button type="button" class="btn btn-sm btn-egy-primary btn-add-stop rounded-pill px-3 ms-3" data-dest-id="@dest.Id">
        <i class="bi bi-plus-lg me-1"></i> @Localizer["TripDetails_AddStop"].Value
    </button>
</div>
```

- Checkbox is the first flex child, before the text.
- Existing `.btn-add-stop` is untouched except for adding `ms-3` for spacing.
- Keep all existing `data-dest-*` attributes.

#### B. Add modal footer
Insert a `.modal-footer` inside `#addStopModal` after the `.modal-body`:

```html
<div class="modal-footer justify-content-between">
    <span class="selected-count small text-muted">0 selected</span>
    <button type="button" class="btn btn-egy-primary" id="addSelectedBtn" disabled>
        @Localizer["TripDetails_AddSelected"].Value
    </button>
</div>
```

#### C. Add resource key
Add `TripDetails_AddSelected` to both resx files (near the other `TripDetails_*` keys):
- `Resources/SharedResource.en.resx`: `<value>Add Selected</value>`
- `Resources/SharedResource.ar.resx`: `<value>إضافة المحدد</value>`

---

### 3. JS Refactor & New Handlers (`@section Scripts`)

#### A. Extract `buildStopCardHtml(stop)`
Move the long inline jQuery template string from the single-add success handler (currently lines 597-635) into a reusable function placed near the top of the IIFE, before the event handlers:

```javascript
function buildStopCardHtml(stop) {
    return '<li class="trip-stop-card p-3 mb-3 rounded-3" data-stop-id="' + stop.stopId + '" data-lat="' + stop.lat + '" data-lng="' + stop.lng + '" data-id="' + stop.destinationId + '" style="background: #ffffff; border: 1px solid #E2E8F0 !important; transition: all 0.2s ease;">' +
        '<div class="d-flex align-items-center gap-3">' +
        '<span class="stop-handle text-muted" title="Drag to reorder" style="cursor: grab; font-size: 1.2rem;"><i class="bi bi-grip-vertical"></i></span>' +
        '<div class="stop-order-badge">' + stop.order + '</div>' +
        '<div class="flex-grow-1">' +
        '<h6 class="fw-bold text-dark mb-0 fs-6">' + _esc(stop.destinationName) + '</h6>' +
        '<small class="text-secondary"><i class="bi bi-geo-alt me-1 text-gold"></i>' + _esc(stop.destinationCity) + '</small>' +
        '</div>' +
        '<div class="stop-dates text-dark small text-end bg-light px-3 py-1 rounded-2 border">' +
        '<div><i class="bi bi-calendar-check me-1 text-gold"></i>Arr: <strong class="text-dark">' + stop.arrivalDate + '</strong></div>' +
        '<div><i class="bi bi-calendar-x me-1 text-gold"></i>Dep: <strong class="text-dark">' + stop.departureDate + '</strong></div>' +
        '</div>' +
        '</div>' +
        '<div class="stop-date-editor d-none mt-3 p-3 bg-light rounded-3 border">' +
        '<div class="row g-2 align-items-end">' +
        '<div class="col">' +
        '<label class="form-label small mb-1 fw-bold text-muted">@Localizer["TripDetails_ArrivalLabel"].Value</label>' +
        '<input type="date" class="form-control form-control-sm edit-arrival fw-semibold text-dark" value="' + stop.arrivalDateInput + '" />' +
        '</div>' +
        '<div class="col">' +
        '<label class="form-label small mb-1 fw-bold text-muted">@Localizer["TripDetails_DepartureLabel"].Value</label>' +
        '<input type="date" class="form-control form-control-sm edit-departure fw-semibold text-dark" value="' + stop.departureDateInput + '" />' +
        '</div>' +
        '<div class="col-auto">' +
        '<button type="button" class="btn btn-sm btn-egy-primary btn-save-dates" data-stop-id="' + stop.stopId + '">' +
        '<i class="bi bi-check-lg me-1"></i> @Localizer["TripDetails_Save"].Value' +
        '</button>' +
        '</div>' +
        '</div>' +
        '</div>' +
        '<div class="stop-actions mt-3 pt-2 border-top d-flex justify-content-end gap-2">' +
        '<button type="button" class="btn btn-sm btn-outline-secondary btn-edit-dates rounded-pill px-3">' +
        '<i class="bi bi-pencil-fill me-1"></i> @Localizer["TripDetails_EditDates"].Value' +
        '</button>' +
        '<button type="button" class="btn btn-sm btn-outline-danger btn-remove-stop rounded-pill px-3" data-stop-id="' + stop.stopId + '">' +
        '<i class="bi bi-trash-fill me-1"></i> @Localizer["TripDetails_Remove"].Value' +
        '</button>' +
        '</div>' +
        '</li>';
}
```

**Byte-for-byte identical** to the current inline string. Only the variable names change from `response.xxx` to `stop.xxx`.

#### B. Update single-add success handler
Replace the inline `$newCard = $('<li ...' + ... + '</li>')` with:

```javascript
var $newCard = $(buildStopCardHtml(response));
$('#stopList').prepend($newCard);
```

Everything else in the handler stays identical.

#### C. Add `updateSelectedCount()` helper
```javascript
function updateSelectedCount() {
    var count = $('#availableDestinationsList .dest-select-checkbox:checked').length;
    $('.selected-count').text(count + ' selected');
    var $btn = $('#addSelectedBtn');
    $btn.prop('disabled', count === 0);
    if (count > 0) {
        $btn.text('@Localizer["TripDetails_AddSelected"].Value' + ' (' + count + ')');
    } else {
        $btn.text('@Localizer["TripDetails_AddSelected"].Value');
    }
}
```

#### D. Add checkbox change handler
```javascript
$('#availableDestinationsList').on('change', '.dest-select-checkbox', function () {
    updateSelectedCount();
});
```

#### E. Extend open-modal handler to reset checkboxes
Modify the existing `#addStopBtn` click handler:

```javascript
$('#addStopBtn').on('click', function () {
    $('#addStopSearchInput').val('');
    $('#addStopCategoryFilter').val('');
    $('#availableDestinationsList .list-group-item').show();
    $('#availableDestinationsList .dest-select-checkbox').prop('checked', false);
    updateSelectedCount();
    $('#addStopModal').modal('show');
});
```

#### F. Hook filter to uncheck hidden items
Modify `filterAddStopDestinations()`:

```javascript
function filterAddStopDestinations() {
    var searchVal = $('#addStopSearchInput').val().toLowerCase().trim();
    var categoryVal = $('#addStopCategoryFilter').val().toLowerCase();

    $('#availableDestinationsList .list-group-item').each(function () {
        var $item = $(this);
        var name = ($item.data('dest-name') || '').toLowerCase();
        var city = ($item.data('dest-city') || '').toLowerCase();
        var category = ($item.data('dest-category') || '').toLowerCase();

        var matchesSearch = name.indexOf(searchVal) !== -1 || city.indexOf(searchVal) !== -1;
        var matchesCategory = categoryVal === '' || category === categoryVal;
        var show = matchesSearch && matchesCategory;

        $item.toggle(show);

        if (!show) {
            $item.find('.dest-select-checkbox').prop('checked', false);
        }
    });
    updateSelectedCount();
}
```

#### G. Add batch "Add Selected" click handler
Place near the single-add handler (after it, or before — order doesn't matter since they're event-bound):

```javascript
$('#addSelectedBtn').on('click', function () {
    var destIds = $('#availableDestinationsList .dest-select-checkbox:checked').map(function () {
        return parseInt($(this).data('dest-id'), 10);
    }).get();

    if (!destIds.length) return;

    $.ajax({
        url: '@Url.Action("AddStops", "Trip", new { id = Model.Id })',
        type: 'POST',
        data: { id: @Model.Id, destinationIds: destIds },
        headers: { 'RequestVerificationToken': token },
        success: function (response) {
            if (response.added && response.added.length) {
                var $emptyState = $('#stopList').siblings('.text-center.py-5');
                if ($emptyState.length) $emptyState.remove();

                response.added.forEach(function (stop) {
                    var $newCard = $(buildStopCardHtml(stop));
                    $('#stopList').prepend($newCard);
                    if (detailsMap && detailsMap.addStopOverlay && stop.lat && stop.lng) {
                        detailsMap.addStopOverlay(stop.lat, stop.lng, '#' + stop.order);
                    }
                });

                var count = $('#stopList li').length;
                $('#stopsCount .badge').text(count + ' Stop' + (count === 1 ? '' : 's'));

                response.added.forEach(function (stop) {
                    $('#availableDestinationsList .list-group-item[data-dest-id="' + stop.destinationId + '"]').remove();
                });

                $('#availableDestinationsList .dest-select-checkbox').prop('checked', false);
                updateSelectedCount();
                $('#addStopModal').modal('hide');
            }
        }
    });
});
```

**Notes**:
- `alreadyExists` items are skipped silently (they were already excluded from the rendered list server-side, so they shouldn't appear in the DOM, but the defensive controller check handles race conditions).
- Rows for successfully added stops are **removed** from `#availableDestinationsList`.
- All checkboxes are reset and count updated after success.

---

## Validation Steps

1. **Build**: `dotnet build` — no errors, no new warnings in edited files.
2. **Manual UI test**:
   - Open `#addStopModal` → search/category filters reset, all checkboxes unchecked, footer shows "0 selected", button disabled.
   - Check 2-3 destinations → footer updates to "3 selected", button enables with "Add Selected (3)".
   - Apply search/category filter that hides a checked row → hidden row becomes unchecked, count decrements.
   - Click "Add Selected" → modal closes, all checked rows disappear from list, new stop cards prepend to `#stopList`, map overlays added, stop count badge updates.
   - Reopen modal → checkboxes reset, list refreshed from server (unadded destinations only).
   - Single-click "Add" on any row → existing behavior preserved: modal closes, one card prepends, row stays in list (current behavior), checkbox state unchanged if it was checked.
3. **Edge case**: Select an item, single-add it, then batch-add the remaining selections. The already-added item's row stays visible (unchanged behavior), batch adds the rest, removes only batch-added rows.

---

## Summary of Constraints Respected

- `AddStop` action is **untouched**.
- Single-click `.btn-add-stop` flow is **untouched** except for delegating card HTML to `buildStopCardHtml()`.
- Drag-reorder, edit-dates, and remove-stop handlers are **untouched**.
- Filter logic is extended only to uncheck hidden items.
- New strings go through `@Localizer[...]` with `TripDetails_*` naming in both `.en.resx` and `.ar.resx`.
- No changes to `TripController.Details` or `ViewBag.AvailableDestinations` scoping.

---

## Layout Adjustment: Sticky Filter Bar with Inline Batch Controls

### Current State
- `.modal-dialog` has only `modal-lg`; modal body scrolls with default browser overflow.
- Filter/search bar sits at the top of `.modal-body` with class `filter-bar`.
- Batch controls ("N selected" + "Add Selected" button) live in a `.modal-footer` below the list.
- JS selectors `$('.selected-count')` and `$('#addSelectedBtn')` target the footer elements.

### Changes Required

#### 1. `Views/Trip/Details.cshtml` — Move batch controls into filter bar

**HTML restructuring:**
- Remove the `<div class="modal-footer justify-content-between">...</div>` entirely.
- Inside the `.filter-bar` div, restructure the existing `.row` from 2 columns to 3 columns:
  - `col-sm-5` — Search input + label
  - `col-sm-4` — Category filter + label
  - `col-sm-3 text-end` — Selected count `<span>` + Add Selected `<button>`
- Keep the existing `@Localizer[...]` strings and `id` attributes (`addStopSearchInput`, `addStopCategoryFilter`, `addSelectedBtn`) unchanged.

**CSS additions:**
- Add `modal-dialog-scrollable` to `.modal-dialog` so `.modal-body` becomes Bootstrap's scroll container.
- Add sticky styling to `.filter-bar`:
  ```css
  .filter-bar {
      position: sticky;
      top: 0;
      z-index: 2;
      background: #F8FAFC; /* already inline, keep it */
      border-bottom: 1px solid #E2E8F0;
  }
  ```
- Remove `mb-3` from `.filter-bar` so there's no persistent gap between the sticky bar and the scrolling list; the new `border-bottom` provides visual separation.

**Why `col-sm-5` / `col-sm-4` / `col-sm-3`:**
- Preserves reasonable width for the search input while fitting the button/count in one row.
- On `< sm`, Bootstrap auto-stacks all three, which is acceptable mobile behavior inside a modal.

**Alternative (if input width is a concern):**
- Keep search/category at current widths (`col-sm-7` + `col-sm-5`) and place the batch controls in a compact sub-row directly below:
  ```html
  <div class="row g-2 align-items-end">
      <!-- existing search/category row -->
  </div>
  <div class="row g-2 align-items-center mt-2">
      <div class="col-12 text-end">
          <span class="selected-count small text-muted me-2">0 selected</span>
          <button type="button" class="btn btn-egy-primary" id="addSelectedBtn" disabled>...</button>
      </div>
  </div>
  ```
- This avoids squeezing inputs but adds ~40px of vertical height to the sticky block.

**JS impact:** None. The selectors `$('.selected-count')` and `$('#addSelectedBtn')` work regardless of DOM position. No handler changes needed.

**Sticky behavior verification:**
- `.filter-bar` is already a direct child of `.modal-body`.
- Adding `modal-dialog-scrollable` makes `.modal-body` the nearest scrolling ancestor (`overflow-y: auto`).
- `position: sticky; top: 0;` on `.filter-bar` will therefore stick to the top of `.modal-body`'s scroll area.
- Opaque background + `z-index: 2` prevents list items from showing through.

### Execution Order

1. Edit `Views/Trip/Details.cshtml`:
   - Add `modal-dialog-scrollable` to `.modal-dialog`.
   - Restructure the `.filter-bar` row to 3 columns and move the footer content inside it.
   - Remove the `.modal-footer` div.
   - Update `.filter-bar` inline styles: remove `mb-3`, add `border-bottom: 1px solid #E2E8F0;` (or move to CSS block).
   - Add sticky CSS rules for `.filter-bar` in the `@section Scripts` `<style>` block.
2. Run `dotnet build` to verify no compilation errors.

### Risks / Edge Cases

- **Narrow modals:** On very small screens the stacked layout may make the filter bar tall, but sticky still works and the user can scroll the list underneath.
- **Z-index conflicts:** Bootstrap modals already use high z-indexes; `.filter-bar` at `z-index: 2` is relative to `.modal-body` (which has `position: relative`), so it stacks above list items without fighting the modal backdrop.
- **Dead CSS:** The existing `.add-stop-filter-bar input/select` rules are currently unused because the HTML lacks that class. Adding `add-stop-filter-bar` to the div would activate them; this is harmless and can be done as part of the edit.

