# Fix: Destination Create form silent failure / blank postback

## Diagnosis

### What the code shows
1. **`DestinationController.Create(POST)`** (`Controllers/DestinationController.cs:126`):
   ```csharp
   public IActionResult Create(Destination destination, double Lat, double Long)
   {
       if (ModelState.IsValid)
       {
           destination.Location = new Point(Long, Lat) { SRID = 4326 };
           destination.Visits = 0;
           _repo.Add(destination);
           _repo.Save();
           return RedirectToAction("Index");   // success → PRG
       }
       return View(destination);              // failure → re-render form
   }
   ```
   - If `ModelState.IsValid` is **false**, the action returns the Create view with the model. **No redirect, no save.**
   - If `ModelState.IsValid` is **true**, it saves and redirects to `/Destination/Index`.

2. **`Views/Destination/Create.cshtml`**:
   - **No `<div asp-validation-summary="All">`** anywhere in the form. ModelState errors for `Name`, `City`, or the `Lat`/`Long` parameters are never rendered to the page.
   - `Lat`/`Long` inputs are **plain HTML** (`<input name="Lat" id="Lat" ...>`) with **no `value` attribute**. On a postback these always render empty because they are not bound to the model.
   - The `@section Scripts` block hardcodes `initialLat: 30.0444, initialLng: 31.2357` and never reads the current input values (unlike `Edit.cshtml` which does `parseFloat(document.getElementById('Lat').value)`).

3. **`Destination` model** (`Models/Destination.cs`):
   - No `[Required]` attributes on `Name`, `City`, `Category`, `Description`, `TicketPrice`.
   - `Location` is a non-nullable `Point` initialized with `null!`, but NRT annotations alone do **not** trigger model-binding validation failures.

### Cross-check against user's symptom
- User sees the **Create page again** after submit → the POST action returned `View(destination)`, which only happens when `ModelState.IsValid == false`.
- **Therefore: a new `Destination` row was NOT created.** This is **Case A** (validation/binding failure, silently swallowed).
- The "page refresh" is the browser re-rendering the returned view.
- "Lat/Long fields come back empty" is because the plain inputs have no `value` binding.
- "No console error" is expected — validation failures are server-side ModelState issues, not JS exceptions.

### Most likely binding failure cause
The form posts `Lat` and `Long` as `double` parameters. If either input is empty or non-numeric on submit, model binding fails for that parameter and sets `ModelState.IsValid = false`. Because there is no validation summary, the user sees no error.

A secondary possibility: the map-picker script runs **after** the page loads and re-initializes the map with hardcoded Cairo coordinates. If the user had previously entered Lat/Long manually and the script overwrote the inputs' visual state (via marker position, not input value), the user might think the fields cleared — but the actual root cause is still the missing validation feedback.

## Fix

All changes are view-only. **No controller or entity-model changes required.**

### 1. Add validation summary to `Create.cshtml`
Add at the top of the `<form>` so any ModelState errors become visible:
```html
<div asp-validation-summary="All" class="text-danger"></div>
```

### 2. Preserve Lat/Long values on postback in `Create.cshtml`
Change the plain inputs to render posted values on failed postback without breaking the existing `name="Lat"`/`name="Long"` binding:
```html
<input name="Lat" id="Lat" class="form-control" type="number" step="any"
       value="@(ViewBag.Lat ?? string.Empty)" />
<input name="Long" id="Long" class="form-control" type="number" step="any"
       value="@ViewBag.Long ?? string.Empty" />
```

Then in the controller POST, when returning the view on invalid ModelState, pass the values back:
```csharp
return View(destination)
{
    // set ViewBag so the view can re-populate the inputs
    // (this is the only controller touch needed)
};
```

Wait — actually, a simpler approach that avoids controller changes: use `Request.Form` in the view, or better yet, just accept that on postback the map picker should read the current input values (which the browser does preserve for `type="number"` inputs via its own form-resubmission behavior in some cases). But the most robust fix is to store them in `ViewData`/`ViewBag` on the failed POST.

Actually, the simplest robust fix without touching the controller: use a hidden field + read from `Context.Request`. But that's hacky.

Better plan: **Add a lightweight view-model property approach.** Since the user explicitly forbade data-model changes in the original plan, but this is purely a view-layer concern, we can use `ViewData` in the controller to pass the coordinates back on a failed post:

In `DestinationController.Create(POST)`:
```csharp
if (!ModelState.IsValid)
{
    ViewBag.Lat = Lat;
    ViewBag.Long = Long;
    return View(destination);
}
```

And in `Create.cshtml`, the inputs read from `ViewBag`:
```html
<input name="Lat" id="Lat" class="form-control" type="number" step="any"
       value="@ViewBag.Lat" />
<input name="Long" id="Long" class="form-control" type="number" step="any"
       value="@ViewBag.Long" />
```

### 3. Fix the map-init script to read posted values (Create.cshtml)
Update the `@section Scripts` block to read the current input values before falling back to Cairo defaults, matching the pattern already used in `Edit.cshtml`:

```html
@section Scripts {
<script>
    (function () {
        var latVal = parseFloat(document.getElementById('Lat').value);
        var lngVal = parseFloat(document.getElementById('Long').value);
        EGYMaps.initLocationPicker({
            mapElId: 'destinationMap',
            latInputId: 'Lat',
            lngInputId: 'Long',
            contextProxyUrl: '/Map/GetDestinationsGeoJson',
            contextStyle: { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.5, fillOpacity: 0.2 },
            initialLat: isNaN(latVal) ? 30.0444 : latVal,
            initialLng: isNaN(lngVal) ? 31.2357 : lngVal
        });
    })();
</script>
}
```

### 4. Add missing per-field validation spans (optional but recommended)
`Category`, `Description`, and `TicketPrice` fields lack `<span asp-validation-for="...">` elements. Add them so errors are visible if validation ever expands:
```html
<span asp-validation-for="Category" class="text-danger"></span>
<span asp-validation-for="Description" class="text-danger"></span>
<span asp-validation-for="TicketPrice" class="text-danger"></span>
```

## Files to edit
1. `Views/Destination/Create.cshtml` — add validation summary, fix Lat/Long inputs, fix script init
2. `Controllers/DestinationController.cs` — add `ViewBag.Lat/Long` on invalid POST return (2 lines)

## Validation steps
1. Run the app, navigate to `/Destination/Create`.
2. Fill the form, leave a required-looking field empty (e.g. Name), submit — confirm validation errors are now visible.
3. Fill all fields, pick a location on the map, submit — confirm redirect to Index.
4. Verify the new row exists in the database with the correct `Location` point.
5. Submit with empty Lat/Long — confirm validation errors show (model-binding failure for `double` parameters).
6. Confirm no `Uncaught ReferenceError: EGYMaps is not defined` in console.

## Risks
- No entity-model or migration changes.
- Only the Create view is affected by this plan; Edit already has the `ViewBag` fallback pattern and preserves values via `value="@Model.Location.Y"`.
- If GeoServer is unreachable, the context WFS layer shows the inline notice but the picker remains fully functional (existing defensive behavior in `maps.js`).
