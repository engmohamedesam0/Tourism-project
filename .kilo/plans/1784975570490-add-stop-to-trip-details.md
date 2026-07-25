# Plan: Add "Add Destination" capability to Trip/Details

## Context

The tourist-facing trip details page (`Views/Trip/Details.cshtml`, served by `TripController.Details`) already supports removing a stop (`DeleteStop`), editing stop dates (`UpdateStopDates`), and drag-reordering stops (`ReorderStops`) — all via small AJAX calls that patch the DOM without a full page reload. This plan adds the matching "add a destination" capability following the exact same conventions.

## Files to modify

1. `Tourist_Project_MVC/Controllers/TripController.cs`
2. `Tourist_Project_MVC/Views/Trip/Details.cshtml`
3. `Tourist_Project_MVC/Resources/SharedResource.en.resx`
4. `Tourist_Project_MVC/Resources/SharedResource.ar.resx`

## 1. Controller — `TripController.cs`

### 1a. New action: `AddStop`

Add a new `[HttpPost, ValidateAntiForgeryToken]` action after `DeleteStop` (around line 338):

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult AddStop(int id, int destinationId)
```

Logic:
1. Resolve tourist via `ResolveTourist()`.
2. Load trip via `_tripPlanRepo.GetByIdWithDetails(id)`.
3. Ownership check: `if (trip == null || trip.TouristId != tourist.Id) return Forbid();`
4. Validate destination exists: `var destination = _destinationRepo.GetById(destinationId); if (destination == null) return NotFound();`
5. Duplicate check: `if (trip.TripDestinations.Any(td => td.DestinationId == destinationId))` → return `Json(new { success = true, alreadyExists = true })`.
6. Otherwise, compute `Visit_Order`: `var maxOrder = trip.TripDestinations.Any() ? trip.TripDestinations.Max(td => td.Visit_Order) : 0;`
7. Create new `TripDestination` with `Visit_Order = maxOrder + 1`, `ArrivalDate = trip.StartDate`, `DepartureDate = trip.EndDate`.
8. Call `_tripPlanRepo.AddStop(tripDestination)` then `_tripPlanRepo.Save()`.
9. Return `Json` with: `success`, the new `TripDestination.Id`, `Visit_Order`, `Destination.Name`, `Destination.City`, `Destination.Location` (Lat/Lng if present), `ArrivalDate` formatted `"yyyy-MM-dd"` and `"MMM dd"`, `DepartureDate` formatted `"yyyy-MM-dd"` and `"MMM dd"`.

### 1b. Update `Details` action

In the existing `Details(int id)` action (around line 229), after loading the trip, add:

```csharp
var existingDestIds = trip.TripDestinations.Select(td => td.DestinationId).ToHashSet();
ViewBag.AvailableDestinations = _destinationRepo.GetAll()
    .Where(d => d.Status == "Active" && !existingDestIds.Contains(d.Id))
    .ToList();
```

## 2. View — `Details.cshtml`

### 2a. "Add Destination" button in card header

In the "RIGHT: Sortable Stop List" card header (lines 97-104), next to the "N Stops" badge, add a button:

```html
<button type="button" class="btn btn-sm btn-egy-primary" data-bs-toggle="modal" data-bs-target="#addStopModal">
    <i class="bi bi-plus-lg me-1"></i> @Localizer["TripDetails_AddStop"].Value
</button>
```

### 2b. Bootstrap modal (place before closing `</div>` of the card, after the stop list)

Add a Bootstrap 5 modal with:
- `id="addStopModal"`, `tabindex="-1"`, `aria-labelledby="addStopModalLabel"`
- Modal header: title from `@Localizer["TripDetails_AddStop"].Value`, close button
- Modal body: a search input (`<input type="text" id="destinationSearch" ...>`) and a list of `ViewBag.AvailableDestinations` rendered as clickable items
- Each item shows destination name + city, with an "Add" button per item
- On "Add": call the AJAX POST (see 2c)
- Modal footer: Cancel button (`data-bs-dismiss="modal"`)

### 2c. AJAX POST for AddStop

In the `@section Scripts` block, add a new delegated click handler for `.btn-add-stop` buttons inside the modal:

```javascript
$('#addStopModal').on('click', '.btn-add-stop', function () {
    var destinationId = $(this).data('destination-id');
    var $btn = $(this);
    $btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm"></span>');

    $.ajax({
        url: '@Url.Action("AddStop", "Trip", new { id = Model.Id })',
        type: 'POST',
        data: { destinationId: destinationId },
        headers: { 'RequestVerificationToken': token },
        success: function (data) {
            if (data.alreadyExists) {
                $('#addStopModal').modal('hide');
                return;
            }
            // Append new <li> to #stopList
            var lat = data.lat;
            var lng = data.lng;
            var order = data.visitOrder;
            var arrivalFormatted = data.arrivalDateMmm;
            var departureFormatted = data.departureDateMmm;
            var arrivalFull = data.arrivalDateYyyy;
            var departureFull = data.departureDateYyyy;

            var $newCard = $(
                '<li class="trip-stop-card p-3 mb-3 rounded-3" ' +
                'data-stop-id="' + data.id + '" ' +
                'data-lat="' + (lat || '') + '" ' +
                'data-lng="' + (lng || '') + '" ' +
                'data-id="' + data.destinationId + '" ' +
                'style="background: #ffffff; border: 1px solid #E2E8F0 !important; transition: all 0.2s ease;">' +
                '<div class="d-flex align-items-center gap-3">' +
                '<span class="stop-handle text-muted" title="Drag to reorder" style="cursor: grab; font-size: 1.2rem;"><i class="bi bi-grip-vertical"></i></span>' +
                '<div class="stop-order-badge">' + order + '</div>' +
                '<div class="flex-grow-1">' +
                '<h6 class="fw-bold text-dark mb-0 fs-6">' + _esc(data.destinationName) + '</h6>' +
                '<small class="text-secondary"><i class="bi bi-geo-alt me-1 text-gold"></i>' + _esc(data.destinationCity) + '</small>' +
                '</div>' +
                '<div class="stop-dates text-dark small text-end bg-light px-3 py-1 rounded-2 border">' +
                '<div><i class="bi bi-calendar-check me-1 text-gold"></i>Arr: <strong class="text-dark">' + arrivalFormatted + '</strong></div>' +
                '<div><i class="bi bi-calendar-x me-1 text-gold"></i>Dep: <strong class="text-dark">' + departureFormatted + '</strong></div>' +
                '</div>' +
                '</div>' +
                '<div class="stop-date-editor d-none mt-3 p-3 bg-light rounded-3 border">' +
                '<div class="row g-2 align-items-end">' +
                '<div class="col"><label class="form-label small mb-1 fw-bold text-muted">@Localizer["TripDetails_ArrivalLabel"].Value</label>' +
                '<input type="date" class="form-control form-control-sm edit-arrival fw-semibold text-dark" value="' + arrivalFull + '" /></div>' +
                '<div class="col"><label class="form-label small mb-1 fw-bold text-muted">@Localizer["TripDetails_DepartureLabel"].Value</label>' +
                '<input type="date" class="form-control form-control-sm edit-departure fw-semibold text-dark" value="' + departureFull + '" /></div>' +
                '<div class="col-auto"><button type="button" class="btn btn-sm btn-egy-primary btn-save-dates" data-stop-id="' + data.id + '"><i class="bi bi-check-lg me-1"></i> @Localizer["TripDetails_Save"].Value</button></div>' +
                '</div></div>' +
                '<div class="stop-actions mt-3 pt-2 border-top d-flex justify-content-end gap-2">' +
                '<button type="button" class="btn btn-sm btn-outline-secondary btn-edit-dates rounded-pill px-3"><i class="bi bi-pencil-fill me-1"></i> @Localizer["TripDetails_EditDates"].Value</button>' +
                '<button type="button" class="btn btn-sm btn-outline-danger btn-remove-stop rounded-pill px-3" data-stop-id="' + data.id + '"><i class="bi bi-trash-fill me-1"></i> @Localizer["TripDetails_Remove"].Value</button>' +
                '</div></li>'
            );

            $('#stopList').append($newCard);

            // Update stop count badge
            var count = $('#stopList li').length;
            $('.badge.rounded-pill').text(count + ' Stop' + (count === 1 ? '' : 's'));

            // Add map overlay if coordinates present
            if (!isNaN(lat) && !isNaN(lng) && detailsMap && detailsMap.addStopOverlay) {
                detailsMap.addStopOverlay(lat, lng, '#' + order);
                detailsMap.fitBounds([[lat, lng]]);
            }

            // Remove the added destination from the modal list
            $btn.closest('.destination-item').remove();

            // Hide modal
            $('#addStopModal').modal('hide');

            // If no items left in the picker, hide the modal body list or show empty state
            if ($('#destinationPicker .destination-item').length === 0) {
                $('#destinationPicker').html('<p class="text-muted text-center py-3 mb-0">@Localizer["TripDetails_NoAvailableDestinations"].Value</p>');
            }
        }
    });
});
```

### 2d. Searchable filter (plain JS)

Add a `keyup` handler on `#destinationSearch` that filters `.destination-item` elements by text content:

```javascript
$('#destinationSearch').on('input', function () {
    var query = $(this).val().toLowerCase();
    $('#destinationPicker .destination-item').each(function () {
        var text = $(this).text().toLowerCase();
        $(this).toggle(text.indexOf(query) !== -1);
    });
});
```

### 2e. Existing handlers remain untouched

The existing `btn-edit-dates`, `btn-save-dates`, and `btn-remove-stop` delegated handlers on `#stopList` will automatically work for newly appended cards since they use event delegation (`$('#stopList').on('click', ...)`).

## 3. Resource strings — add to both `.resx` files

### `SharedResource.en.resx` (add before closing `</root>`):

```xml
<data name="TripDetails_AddStop" xml:space="preserve">
  <value>Add Destination</value>
</data>
<data name="TripDetails_NoAvailableDestinations" xml:space="preserve">
  <value>No destinations available to add.</value>
</data>
```

### `SharedResource.ar.resx` (add before closing `</root>`):

```xml
<data name="TripDetails_AddStop" xml:space="preserve">
  <value>إضافة وجهة</value>
</data>
<data name="TripDetails_NoAvailableDestinations" xml:space="preserve">
  <value>لا توجد وجهات متاحة للإضافة.</value>
</data>
```

## 4. JSON response shape from AddStop

The controller returns JSON with these fields for the client:

| Field | Type | Description |
|-------|------|-------------|
| `success` | bool | Always true on success |
| `alreadyExists` | bool | True if destination was already in trip |
| `id` | int | New TripDestination.Id |
| `visitOrder` | int | Visit_Order value |
| `destinationName` | string | Destination.Name |
| `destinationCity` | string | Destination.City |
| `lat` | double? | Destination.Location.Y if present |
| `lng` | double? | Destination.Location.X if present |
| `arrivalDateYyyy` | string | ArrivalDate as "yyyy-MM-dd" |
| `arrivalDateMmm` | string | ArrivalDate as "MMM dd" |
| `departureDateYyyy` | string | DepartureDate as "yyyy-MM-dd" |
| `departureDateMmm` | string | DepartureDate as "MMM dd" |
| `destinationId` | int | The DestinationId |

## 5. Key design decisions

- **Modal + per-item Add button** (not a select + single submit): Simpler, consistent with the site's existing patterns, and avoids needing a separate "Add" submit button. Each destination card in the modal has its own "Add" button.
- **Plain JS search filter**: No new library needed; a simple `keyup` handler on the search input filters the list by text content.
- **Event delegation for existing handlers**: The `btn-edit-dates`, `btn-save-dates`, and `btn-remove-stop` handlers are already bound via `$('#stopList').on('click', ...)`, so newly appended cards work automatically.
- **Map overlay**: Uses the same `detailsMap.addStopOverlay` / `detailsMap.fitBounds` pattern as `onLayerReady`.
- **Remove from picker**: After a successful add, the destination item is removed from the modal list so it can't be added twice from the UI.

## 6. What is NOT changed

- `TripPlanController.cs` — not touched
- `Views/TripPlan/*` — not touched
- `ReorderStops`, `UpdateStopDates`, `DeleteStop` actions — not modified
- Existing JS for those three actions — not modified
- Admin-facing trip pages — not touched

## 7. Validation steps

1. Build the project (`dotnet build`) to verify no compilation errors.
2. Run the app and navigate to a trip details page.
3. Verify the "Add Destination" button appears in the card header.
4. Click the button — modal opens with the list of available destinations.
5. Type in the search box — list filters correctly.
6. Click "Add" on a destination — modal closes, new stop card appears in the list, badge count updates, map shows the new marker (if coordinates exist).
7. Try adding the same destination again — it should not duplicate (either the modal closes silently or the server returns `alreadyExists: true`).
8. Verify existing ReorderStops, UpdateStopDates, DeleteStop still work.
9. Verify localization works by switching language.