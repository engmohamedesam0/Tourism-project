# Fix: "EGYMaps is not defined" on Destination/SponsorBranch forms

## Status of previous fix
The `_Layout.cshtml` script-order fix (`RenderSectionAsync("Scripts")` moved after `maps.js`) resolved the error on pages that use proper `@section Scripts { ... }` blocks:
- ✅ Explore
- ✅ Trip/Index
- ✅ Trip/Details
- ✅ NearMe

## New diagnosis
The error **still occurs** on:
- `Destination/Create.cshtml`
- `Destination/Edit.cshtml`
- `SponsorBranch/Create.cshtml`
- `SponsorBranch/Edit.cshtml`

**Root cause:** These four views place their `EGYMaps` init calls inside **raw inline `<script>` tags directly in the page body/form markup**, not wrapped in `@section Scripts { ... }`. The browser executes inline scripts immediately when it encounters them during HTML parsing — before it ever reaches the layout footer where `maps.js` is loaded. So even though the layout now has the correct order, these inline scripts run too early.

**Confirmed single layout:** `_ViewStart.cshtml` sets `Layout = "_Layout"` for all views. There is no separate Admin layout. The bug is purely the inline-vs-section pattern mismatch.

## Current broken pattern (all four views)
```html
<!-- inline in body, executes immediately -->
<script>
    (function () {
        EGYMaps.initLocationPicker({ ... });
    })();
</script>
```

## Fix
Wrap each inline map-init script in a proper `@section Scripts { ... }` block, exactly matching the pattern used on Explore/NearMe/Trip pages.

### Files to edit

**1. `Views/Destination/Create.cshtml`**
- Remove the inline `<script>...</script>` block at lines 61-73 (after `</div>` and before `}`)
- Add at end of file, inside the existing `@if` block:
```csharp
    }
}
```
Change to:
```csharp
    }

    @section Scripts {
    <script>
        (function () {
            EGYMaps.initLocationPicker({
                mapElId: 'destinationMap',
                latInputId: 'Lat',
                lngInputId: 'Long',
                contextProxyUrl: '/Map/GetDestinationsGeoJson',
                contextStyle: { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.5, fillOpacity: 0.2 },
                initialLat: 30.0444,
                initialLng: 31.2357
            });
        })();
    </script>
}
```

**2. `Views/Destination/Edit.cshtml`**
- Remove the inline `<script>...</script>` block at lines 74-88
- Add at end of file, inside the existing `@if` block:
```csharp
    }

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

**3. `Views/SponsorBranch/Create.cshtml`**
- Remove the inline `<script>...</script>` block at lines 55-67 (note: current file has an extra `</div>` at line 68 which should stay)
- Add at end of file, inside the existing root markup:
```html
    @section Scripts {
    <script>
        (function () {
            EGYMaps.initLocationPicker({
                mapElId: 'branchMap',
                latInputId: 'Lat',
                lngInputId: 'Long',
                contextProxyUrl: '/Map/GetBranchesGeoJson',
                contextStyle: { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.5, fillOpacity: 0.2 },
                initialLat: 30.0444,
                initialLng: 31.2357
            });
        })();
    </script>
}
```

**4. `Views/SponsorBranch/Edit.cshtml`**
- Remove the inline `<script>...</script>` block at lines 58-72
- Add at end of file:
```html
    @section Scripts {
    <script>
        (function () {
            var latVal = parseFloat(document.getElementById('Lat').value);
            var lngVal = parseFloat(document.getElementById('Long').value);
            EGYMaps.initLocationPicker({
                mapElId: 'branchMap',
                latInputId: 'Lat',
                lngInputId: 'Long',
                contextProxyUrl: '/Map/GetBranchesGeoJson',
                contextStyle: { radius: 6, fillColor: '#888', color: '#555', weight: 1, opacity: 0.5, fillOpacity: 0.2 },
                initialLat: isNaN(latVal) ? 30.0444 : latVal,
                initialLng: isNaN(lngVal) ? 31.2357 : lngVal
            });
        })();
    </script>
}
```

## Verification
1. Run the app and open each of the four pages:
   - `/Destination/Create`
   - `/Destination/Edit/{id}`
   - `/SponsorBranch/Create`
   - `/SponsorBranch/Edit/{id}`
2. Confirm no `Uncaught ReferenceError: EGYMaps is not defined` in the browser console.
3. Confirm the map initializes and the draggable marker / click-to-place behavior works.
4. Confirm typing in Lat/Long inputs moves the marker, and clicking the map updates the inputs.
5. Confirm existing save flows still persist `Location` correctly (no controller changes were made).

## Risks / notes
- No controller or model changes are needed — this is purely a view-script-placement fix.
- The `_Layout.cshtml` script-order fix is already applied and remains in place; this plan only addresses the four views that bypass it via inline scripts.
- After this fix, all eight map-consuming pages use the same `@section Scripts` pattern and will initialize correctly.
