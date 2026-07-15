# Fix: Administration dropdown shows per-item badges + aggregate is sum of children

## Problem
The Administration nav dropdown shows an aggregate badge on its toggle (from `AdminApprovalBell`), but the dropdown items themselves carry no badges. Admins cannot tell at a glance whether the count comes from Approvals, Support Inbox, or elsewhere.

## Root cause
`AdminApprovalBellViewComponent` only renders one aggregate badge on the parent toggle. There is no per-item badge on Approvals or Support Inbox, and no Support Inbox count ViewComponent exists for the admin nav.

## Fixes

### 1. Create `AdminNavBadgesVM`
**File:** `Tourist_Project_MVC/View_Model/AdminNavBadgesVM.cs` (new)

```csharp
namespace Tourist_Project_MVC.View_Model
{
    public class AdminNavBadgesVM
    {
        public int PendingApprovalsCount { get; set; }
        public int UnresolvedSupportCount { get; set; }
    }
}
```

### 2. Create `AdminNavBadgesViewComponent`
**File:** `Tourist_Project_MVC/ViewComponents/AdminNavBadgesViewComponent.cs` (new)

- Inject `TouristContext`.
- `PendingApprovalsCount` = `_context.SponsorApprovalRequests.Count(r => r.Status == "Pending")`.
- `UnresolvedSupportCount` = `_context.SupportTickets.Count(t => t.Status != "Resolved")` (Option A: includes Open + In Progress).
- Return `View("Default", new AdminNavBadgesVM { ... })`.

### 3. Create `AdminNavBadges` ViewComponent view
**File:** `Tourist_Project_MVC/Views/Shared/Components/AdminNavBadges/Default.cshtml` (new)

Render **two** small pill badges (same red/danger style as the existing toggle badge), placed at the end of each dropdown row:

- **Approvals row** (`asp-controller="SponsorApproval"`): badge showing `Model.PendingApprovalsCount`.
- **Support Inbox row** (`asp-controller="AdminSupport"`): badge showing `Model.UnresolvedSupportCount`.

Do **not** render a badge when the count is 0.

### 4. Update `_Layout.cshtml`
**File:** `Tourist_Project_MVC/Views/Shared/_Layout.cshtml`

Inside the Administration dropdown:

1. **Replace** `@await Component.InvokeAsync("AdminApprovalBell")` with `@await Component.InvokeAsync("AdminNavBadges")`.
2. The **parent toggle's badge** must equal the sum of its children's badges. Since the ViewComponent renders the aggregate, change the toggle badge markup to use the aggregate from the new ViewComponent (e.g. `Model.PendingApprovalsCount + Model.UnresolvedSupportCount`). The ViewComponent view should expose this sum in the VM so the layout can render it on the toggle.
3. **Add per-item badges** next to each dropdown item:
   - Approvals (`SponsorApproval/Index`): `<span class="badge rounded-pill text-bg-danger ms-auto">@Model.PendingApprovalsCount</span>`
   - Support Inbox (`AdminSupport/Index`): `<span class="badge rounded-pill text-bg-danger ms-auto">@Model.UnresolvedSupportCount</span>`
4. Keep the existing badge CSS class (`text-bg-danger`, `rounded-pill`) so it matches the current toggle badge style.

### 5. Apply the same pattern to any other grouped dropdown with an aggregate badge
Checked all nav dropdowns:

- **Admin** → Administration (fixed above).
- **Sponsor** → no dropdown; individual links + `SupportBell` component (its own dropdown with live ticket list, not aggregate counts). No change needed.
- **Tourist** → no aggregate badges.
- **Guest** → no aggregate badges.

No other dropdown needs this fix.

## What does NOT change
- The underlying queries that compute `PendingCount` and `UnresolvedCount` remain in the new ViewComponent. No controller or service changes.
- `AdminApprovalBellViewComponent` and `AdminApprovalBellVM` become unused and can be left for a follow-up cleanup, but should **not** be deleted in this change to keep the diff focused.

## Validation
1. `dotnet build` → 0 errors.
2. Run the app as an Admin:
   - Open the Administration dropdown.
   - Confirm the parent toggle badge equals the sum of the two per-item badges.
   - Confirm Approvals shows its own count and links to `/SponsorApproval`.
   - Confirm Support Inbox shows its own count and links to `/AdminSupport`.
   - Confirm both badges disappear when their counts are 0.
3. Confirm Sponsor, Tourist, and Guest navs are visually unchanged.
