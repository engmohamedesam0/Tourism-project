# Sponsor Registration Overhaul — Completion Plan

## Summary / Status
Almost the entire spec is **already implemented** in the codebase. The only functional
gap is a missing Admin notification bell view component that the shared layout invokes,
which currently throws for any Admin session. This plan documents what already exists
(so the end-to-end flow is verifiable) and the single piece that must be added.

No model changes are required, so **no new migration** is needed — the one focused
migration `Migrations/20260708122012_mig.cs` already creates `SponsorApprovalRequests`
and adds `FirstName/LastName/Nationality/ProfilePicturePath` to `AspNetUsers`, and the
`TouristContextModelSnapshot` matches.

## Already implemented (verified)
- **Registration form + VM**: `View Model/RegisterViewModel.cs` (AccountType, FirstName,
  LastName, PhoneNumber, Nationality, optional ProfilePicture) and
  `Views/Account/Register.cshtml` (radio Tourist/Sponsor + new fields + optional file upload).
- **ApplicationUser**: `Models/ApplicationUser.cs` adds `FirstName`, `LastName`,
  `Nationality`, `ProfilePicturePath` (nullable). Reuses built-in `PhoneNumber`.
- **Register POST**: `Controllers/AccountController.cs:37` creates the user, saves the
  optional image under `wwwroot/uploads/profile-pictures/` (validates image ext + 2 MB),
  then branches:
  - **Tourist**: assigns `"User"` role and links/creates `Tourist` populating
    `Name = FirstName + LastName`, `Nationality`, `Email`.
  - **Sponsor**: creates `SponsorApprovalRequest` (Status `Pending`, `RequestedDate`),
    assigns **no** role, redirects to `SponsorApprovalStatus?status=submitted`.
- **Login gate**: `Controllers/AccountController.cs:133` checks for a
  `SponsorApprovalRequest` on the account; `Pending` → `SponsorApprovalStatus?status=pending`,
  `Rejected` → `...status=rejected`, before any sign-in / portal redirect.
  Approved sponsors flow on to `SponsorPortal` which routes a role-without-Sponsor-record
  user into the existing `CompleteProfile` flow (`SponsorPortalController`).
- **Status page**: `Views/Account/SponsorApprovalStatus.cshtml` (submitted/pending/rejected).
- **Admin approval page**: `Controllers/SponsorApprovalController.cs` (`[Authorize(Roles="Admin")]`)
  with `Index` (list via `SponsorApprovalListItem`), `Approve` (assigns `"Sponsor"` role,
  sets `Approved`/`ReviewedDate`/`ReviewedByAdminId`), `Reject` (sets `Rejected` + review
  fields, leaves account role-less). View: `Views/SponsorApproval/Index.cshtml` with
  status badges + Approve/Reject forms.
- **Entity + context**: `Models/SponsorApprovalRequest.cs` and
  `Data/TouristContext.cs` `DbSet<SponsorApprovalRequest> SponsorApprovalRequests`, with
  FK config in `OnModelCreating`.
- **Existing "complete your sponsor profile" flow**: `Views/SponsorPortal/CompleteProfile.cshtml`
  + `SponsorPortalController.CompleteProfile` is reused as-is (not modified).

## The one missing piece: AdminApprovalBell view component
`Views/Shared/_Layout.cshtml:360` calls `@await Component.InvokeAsync("AdminApprovalBell")`
inside the Admin nav block, but no such component exists → runtime error for Admin users.

### Task 1 — View model
Create `View Model/AdminApprovalBellVM.cs`:
```csharp
namespace Tourist_Project_MVC.View_Model
{
    public class AdminApprovalBellVM
    {
        public int PendingCount { get; set; }
    }
}
```

### Task 2 — View component
Create `ViewComponents/AdminApprovalBellViewComponent.cs` (mirrors
`NotificationBellViewComponent` naming; the component name becomes `AdminApprovalBell`):
```csharp
using Microsoft.AspNetCore.Mvc;
using Tourist_Project_MVC.Data;
using Tourist_Project_MVC.View_Model;

namespace Tourist_Project_MVC.ViewComponents
{
    public class AdminApprovalBellViewComponent : ViewComponent
    {
        private readonly TouristContext _context;
        public AdminApprovalBellViewComponent(TouristContext context) => _context = context;

        public IViewComponentResult Invoke()
        {
            var pending = _context.SponsorApprovalRequests.Count(r => r.Status == "Pending");
            return View("Default", new AdminApprovalBellVM { PendingCount = pending });
        }
    }
}
```

### Task 3 — Component view
Create `Views/Shared/Components/AdminApprovalBell/Default.cshtml` — a nav-item link with a
badge driven by the pending count, reusing existing `nav-link` styling and the status-badge
pattern already used in `SponsorApproval/Index.cshtml`:
```cshtml
@model Tourist_Project_MVC.View_Model.AdminApprovalBellVM
<li class="nav-item">
    <a class="nav-link position-relative" asp-controller="SponsorApproval" asp-action="Index">
        <i class="bi bi-bell-fill"></i> Approvals
        @if (Model.PendingCount > 0)
        {
            <span class="badge rounded-pill text-bg-danger ms-1">@Model.PendingCount</span>
        }
    </a>
</li>
```
This satisfies "simple badge count + list on an Admin nav item pointing at the approval
page" and reuses (not duplicates) the Sponsor portal notification pattern.

## Open design note (per spec)
Rejected sponsors are intentionally left **without any role** and blocked at login (item 2).
Spec says default to leaving them role-less; flagging that assigning `"User"`/Tourist could
be a friendlier fallback. **No change** unless you request it.

## Verification (end-to-end)
1. `dotnet build` in `Tourist_Project_MVC-main/Tourist_Project_MVC` — must compile; Admin
   pages must no longer error (the missing component is now present).
2. **Tourist sign-up**: register as Tourist → role `User` assigned, `Tourists` row created
   with `Name`/`Nationality` populated, redirected to `Explore`. New fields persisted.
3. **Sponsor sign-up**: register as Sponsor → no role, `SponsorApprovalRequest` (Pending)
   created, image saved (or null). Shown `submitted` status page.
4. **Sponsor login while pending**: redirected to `pending` status page; never reaches a portal.
5. **Admin**: logs in, sees Approvals nav item with badge = pending count; opens
   `SponsorApproval/Index`, sees applicant name/email/phone/nationality/date.
6. **Approve**: request → `Approved` with review fields; user gets `Sponsor` role. Next login
   routes into `SponsorPortal` → `CompleteProfile` (existing flow) → `Sponsor` record created.
7. **Reject**: request → `Rejected`; login shows `rejected` status page, no portal access.

## Files / entities touched (final)
- `View Model/AdminApprovalBellVM.cs` (new)
- `ViewComponents/AdminApprovalBellViewComponent.cs` (new)
- `Views/Shared/Components/AdminApprovalBell/Default.cshtml` (new)
- Already present and unchanged: `RegisterViewModel.cs`, `Register.cshtml`, `ApplicationUser.cs`,
  `AccountController.cs`, `SponsorApprovalController.cs`, `SponsorApproval/Index.cshtml`,
  `Account/SponsorApprovalStatus.cshtml`, `SponsorApprovalRequest.cs`, `TouristContext.cs`,
  `SponsorApprovalListItem.cs`, `Migrations/20260708122012_mig.cs` (+snapshot).
