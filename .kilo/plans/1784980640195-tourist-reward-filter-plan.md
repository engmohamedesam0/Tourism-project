# Plan: Add client-side filter bar to TouristReward/Index.cshtml

## Implementation-Ready Patch

Source edits are blocked in this plan mode. The exact file contents to apply are below.

### 1. `Views/TouristReward/Index.cshtml`

**A. Insert filter bar** after line 35 (`</div>` of points balance), before the `<h4>` "Available rewards" heading.

Replace:
```html
     </div>

     <!-- Available rewards -->
     <h4 class="mb-3" style="font-family:'Cinzel',serif; color: var(--egy-dark);">
```

With:
```html
     </div>

     <!-- Available rewards filter bar -->
     <div class="filter-bar mb-3 p-3 rounded-3" style="background: #F8FAFC; border: 1px solid #E2E8F0;">
         <div class="row g-2 align-items-end">
             <div class="col-md-5">
                 <label class="form-label fw-bold small text-uppercase text-muted mb-1">@Localizer["Reward_SearchPlaceholder"].Value</label>
                 <div class="input-group input-group-sm">
                     <span class="input-group-text bg-white border-end-0 text-muted"><i class="bi bi-search"></i></span>
                     <input type="text" id="rewardSearchInput" class="form-control border-start-0" placeholder="@Localizer["Reward_SearchPlaceholder"].Value" />
                 </div>
             </div>
             <div class="col-md-4">
                 <label class="form-label fw-bold small text-uppercase text-muted mb-1">@Localizer["Reward_FilterByType"].Value</label>
                 <select id="rewardTypeFilter" class="form-select form-select-sm">
                     <option value="">@Localizer["Reward_AllTypes"].Value</option>
                     @foreach (var type in Model.AvailableRewards.Select(r => r.RewardType).Distinct().OrderBy(t => t))
                     {
                         <option value="@type">@type</option>
                     }
                 </select>
             </div>
             <div class="col-md-3">
                 <label class="form-label fw-bold small text-uppercase text-muted mb-1">@Localizer["Reward_MaxPoints"].Value</label>
                 <input type="number" id="rewardMaxPoints" class="form-control form-control-sm" min="0" placeholder="@Localizer["Reward_MaxPointsPlaceholder"].Value" />
             </div>
         </div>
     </div>

     <h4 class="mb-3" style="font-family:'Cinzel',serif; color: var(--egy-dark);">
```

**B. Add `data-*` attributes to card wrapper.**
Replace:
```html
                 <div class="col-md-6 col-lg-4">
```
With:
```html
                 <div class="col-md-6 col-lg-4"
                      data-title="@reward.Title.ToLower()"
                      data-description="@reward.Description.ToLower()"
                      data-sponsor="@(reward.Sponsor?.Name ?? "").ToLower()"
                      data-type="@reward.RewardType.ToLower()"
                      data-points="@reward.PointsRequired">
```

**C. Add empty state inside the `else` block, before `<div class="row g-3">`.**

Replace:
```html
     else
     {
         <div class="row g-3">
```

With:
```html
     else
     {
         <div id="rewardEmptyFilter" class="text-muted small p-3 text-center" style="display:none">
             <i class="bi bi-exclamation-circle text-gold fs-4 d-block mb-1"></i>
             @Localizer["Reward_NoMatch"].Value
         </div>
         <div class="row g-3">
```

**D. Add `@section Scripts` at the very end of the file.**

Append:
```html
@section Scripts {
<script>
    (function () {
        var searchInput = document.getElementById('rewardSearchInput');
        var typeSelect = document.getElementById('rewardTypeFilter');
        var maxPointsInput = document.getElementById('rewardMaxPoints');
        var cards = document.querySelectorAll('.col-md-6.col-lg-4[data-title]');
        var emptyEl = document.getElementById('rewardEmptyFilter');

        function matches() {
            var query = (searchInput.value || '').toLowerCase();
            var type = (typeSelect.value || '').toLowerCase();
            var maxPts = maxPointsInput.value !== '' ? parseInt(maxPointsInput.value, 10) : null;
            var visibleCount = 0;

            cards.forEach(function (card) {
                var title = card.getAttribute('data-title') || '';
                var desc = card.getAttribute('data-description') || '';
                var sponsor = card.getAttribute('data-sponsor') || '';
                var cardType = card.getAttribute('data-type') || '';
                var pts = parseInt(card.getAttribute('data-points') || '0', 10);

                var textMatch = !query ||
                    title.indexOf(query) !== -1 ||
                    desc.indexOf(query) !== -1 ||
                    sponsor.indexOf(query) !== -1;

                var typeMatch = !type || cardType === type;
                var pointsMatch = maxPts === null || pts <= maxPts;

                var show = textMatch && typeMatch && pointsMatch;
                card.style.display = show ? '' : 'none';
                if (show) visibleCount++;
            });

            if (emptyEl) {
                emptyEl.style.display = visibleCount === 0 ? '' : 'none';
            }
        }

        var debounceTimer;
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(matches, 180);
            });
        }

        if (typeSelect) typeSelect.addEventListener('change', matches);
        if (maxPointsInput) maxPointsInput.addEventListener('input', matches);
    })();
</script>
}
```

### 2. `Resources/SharedResource.en.resx`

Append these 6 entries near the existing `Reward_*` block (after line 599):

```xml
<data name="Reward_SearchPlaceholder" xml:space="preserve">
  <value>Search rewards...</value>
</data>
<data name="Reward_FilterByType" xml:space="preserve">
  <value>Filter by type</value>
</data>
<data name="Reward_AllTypes" xml:space="preserve">
  <value>All types</value>
</data>
<data name="Reward_MaxPoints" xml:space="preserve">
  <value>Max points</value>
</data>
<data name="Reward_MaxPointsPlaceholder" xml:space="preserve">
  <value>Any</value>
</data>
<data name="Reward_NoMatch" xml:space="preserve">
  <value>No rewards match your filters</value>
</data>
```

### 3. `Resources/SharedResource.ar.resx`

Append these 6 entries near the existing `Reward_*` block (after line 599):

```xml
<data name="Reward_SearchPlaceholder" xml:space="preserve">
  <value>ابحث عن المكافآت...</value>
</data>
<data name="Reward_FilterByType" xml:space="preserve">
  <value>تصفية حسب النوع</value>
</data>
<data name="Reward_AllTypes" xml:space="preserve">
  <value>جميع الأنواع</value>
</data>
<data name="Reward_MaxPoints" xml:space="preserve">
  <value>أقصى نقاط</value>
</data>
<data name="Reward_MaxPointsPlaceholder" xml:space="preserve">
  <value>الكل</value>
</data>
<data name="Reward_NoMatch" xml:space="preserve">
  <value>لا توجد مكافآت تطابق عوامل التصفية</value>
</data>
```

## Validation
1. Build succeeds (`dotnet build`).
2. "/TouristReward/Index" renders the filter bar above the rewards grid with type dropdown populated from distinct `Model.AvailableRewards.Select(r => r.RewardType).Distinct()`.
3. Typing in search input filters by Title + Description + Sponsor name, case-insensitive, debounced ~180ms.
4. Selecting a type filters to only that type; "All types" clears the filter.
5. Entering a max-points value hides cards where `PointsRequired > value`; clearing the input shows all.
6. When all cards are hidden, the "No rewards match your filters" empty state appears.
7. Existing redemption table is unaffected.
8. Existing controller/VM code is untouched.
