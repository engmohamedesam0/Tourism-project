# Plan: Add search + filter to Trip Details "Add Destination" modal

## Context
`Views/Trip/Details.cshtml` has an `#addStopModal` whose `#availableDestinationsList` is a plain scrollable list of `.list-group-item` elements populated server-side from `ViewBag.AvailableDestinations`. With a large catalog, tourists cannot easily find a destination. We will add lightweight client-side search + category filter matching the existing `#destSearchInput` pattern in `Views/Trip/Index.cshtml`.

## Constraints
- Do not touch `TripController.cs` (data already correctly scoped).
- Do not change `#stopList` / map / reorder / remove-stop / edit-dates functionality.
- New strings go through `@Localizer[...]` with new keys following `TripDetails_*` naming.
- Styling: Bootstrap 5 `form-control` / `form-select` only; no new CSS frameworks.

## Files to change
1. `Views/Trip/Details.cshtml` — view markup + JS
2. `Resources/SharedResource.en.resx` — new `TripDetails_*` keys
3. `Resources/SharedResource.ar.resx` — matching Arabic translations

## Implementation tasks

### 1. View markup — `Views/Trip/Details.cshtml`
Inside `#addStopModal` → `.modal-body`, **above** `#availableDestinationsList` (and above the existing `@if (ViewBag.AvailableDestinations ...)` block):

- Add filter controls container:
  ```html
  <div class="row g-2 mb-3">
      <div class="col-sm-5">
          <label class="form-label fw-bold small text-uppercase text-muted mb-1">@Localizer["TripDetails_SearchLabel"].Value</label>
          <div class="input-group input-group-sm">
              <span class="input-group-text bg-white border-end-0 text-muted"><i class="bi bi-search"></i></span>
              <input type="text" id="addStopSearchInput" class="form-control border-start-0" placeholder='@Localizer["TripDetails_SearchPlaceholder"].Value' />
          </div>
      </div>
      <div class="col-sm-4">
          <label class="form-label fw-bold small text-uppercase text-muted mb-1">@Localizer["TripDetails_CategoryLabel"].Value</label>
          <select id="addStopCategoryFilter" class="form-select form-select-sm">
              <option value="">@Localizer["TripDetails_AllCategories"].Value</option>
              @foreach (var cat in (ViewBag.AvailableDestinations as IEnumerable<Tourist_Project_MVC.Models.Destination>)
                  .Where(d => !string.IsNullOrEmpty(d.Category))
                  .Select(d => d.Category)
                  .Distinct()
                  .OrderBy(c => c))
              {
                  <option value="@cat">@cat</option>
              }
          </select>
      </div>
  </div>
  ```
- Add empty-state paragraph (hidden by default), placed **above** `#availableDestinationsList` but **below** the filter controls:
  ```html
  <div id="addStopFilterEmpty" class="text-muted small p-3 text-center" style="display:none">
      <i class="bi bi-exclamation-circle fs-4 d-block mb-1"></i>
      @Localizer["TripDetails_NoFilterMatch"].Value
  </div>
  ```
- On each `.list-group-item` inside `#availableDestinationsList`, add `data-dest-name`, `data-dest-city`, and `data-dest-category` attributes alongside the existing `data-dest-id`, `data-dest-lat`, `data-dest-lng`:
  ```html
  <div class="list-group-item ..."
       data-dest-id="@dest.Id"
       data-dest-name="@dest.Name.ToLower()"
       data-dest-city="@dest.City.ToLower()"
       data-dest-category="@(string.IsNullOrEmpty(dest.Category) ? "" : dest.Category.ToLower())"
       data-dest-lat="..." data-dest-lng="...">
  ```

### 2. JavaScript — same `@section Scripts` block in `Views/Trip/Details.cshtml`
Add a new filtering IIFE (or extend the existing `(function () { ... })();`) near the existing `#addStopModal` handlers.

Logic:
- Select `.list-group-item` children of `#availableDestinationsList`.
- On `input` of `#addStopSearchInput` and `change` of `#addStopCategoryFilter`, iterate items:
  - Visible if `(search text is empty OR item contains search text in `data-dest-name` OR `data-dest-city`)` AND `(selected category is empty OR item `data-dest-category` matches)`.
  - Set `item.style.display = match ? '' : 'none'`.
- Show/hide `#addStopFilterEmpty` based on whether any `.list-group-item:not([style*="none"])` remain visible.
- In `#addStopBtn` click handler, reset filters before showing modal:
  ```js
  $('#addStopSearchInput').val('');
  $('#addStopCategoryFilter').val('');
  applyAddStopFilter(); // optional: clear any previous hidden state
  ```
- Do **not** restructure the existing `.btn-add-stop` handler. The AJAX success path already hides/removes the added item; filtering should remain orthogonal.

### 3. Resource keys
Add to both `SharedResource.en.resx` and `SharedResource.ar.resx`, near the existing `TripDetails_AddStop` / `TripDetails_NoAvailableDestinations` keys:

English:
```xml
<data name="TripDetails_SearchLabel" xml:space="preserve"><value>Search</value></data>
<data name="TripDetails_SearchPlaceholder" xml:space="preserve"><value>Search destinations by name or city...</value></data>
<data name="TripDetails_CategoryLabel" xml:space="preserve"><value>Category</value></data>
<data name="TripDetails_AllCategories" xml:space="preserve"><value>All categories</value></data>
<data name="TripDetails_NoFilterMatch" xml:space="preserve"><value>No destinations match your search.</value></data>
```

Arabic:
```xml
<data name="TripDetails_SearchLabel" xml:space="preserve"><value>بحث</value></data>
<data name="TripDetails_SearchPlaceholder" xml:space="preserve"><value>ابحث عن الوجهات بالاسم أو المدينة...</value></data>
<data name="TripDetails_CategoryLabel" xml:space="preserve"><value>الفئة</value></data>
<data name="TripDetails_AllCategories" xml:space="preserve"><value>جميع الفئات</value></data>
<data name="TripDetails_NoFilterMatch" xml:space="preserve"><value>لا توجد وجهات تطابق البحث.</value></data>
```

## Validation
- `dotnet build` succeeds with 0 errors.
- Open `/Trip/Details/{id}`, click "Add Stop", confirm filter controls render.
- Type in search box: list filters in real time, empty state appears when no matches.
- Change category: list filters correctly, combined with search text.
- Click "Add Stop" on a filtered item: AJAX success adds stop to trip and closes modal; next modal open resets filters and shows full list.
- Existing reorder / edit-dates / remove-stop functionality is unaffected.

## Risks / Edge cases
- **Empty `Category` values**: excluded from the category dropdown via `Where(d => !string.IsNullOrEmpty(d.Category))`. Items with no category will still match "All categories" and search text, but will never match a specific category filter (expected behavior).
- **RTL layout**: Bootstrap `form-select` and `input-group` handle RTL natively via existing `rtl.css`.
- **Stale filters**: reset in `#addStopBtn` click handler prevents hidden items on subsequent opens.
