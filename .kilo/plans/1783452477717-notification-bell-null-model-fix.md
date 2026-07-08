# Fix: NullReferenceException in NotificationBell ViewComponent

## Status
Both root causes from the report are real and must be fixed.

## Confirmed Findings

- **`_Layout.cshtml:531`** invokes `@await Component.InvokeAsync("NotificationBell")` unconditionally, outside any role guard. It executes for every visitor (Admin, Tourist, anonymous), not just Sponsors.
- **`ViewComponents/NotificationBellViewComponent.cs`** `Invoke()` returns `View("Default")` (no model) on two paths:
  - line 22: `!User.IsInRole("Sponsor")` (Admin/Tourist/anonymous)
  - line 30: `userId == 0` (Sponsor with no linked record — e.g. `ApplicationUserId` nulled)
  An unmodeled `View()` makes `Model` null, which crashes `Default.cshtml:11` (`@if (Model.UnreadCount > 0)`).

## Fix (small, contained — does NOT restructure notifications)

### 1. `_Layout.cshtml` — wrap the invocation in a Sponsor guard
```razor
@if (User.IsInRole("Sponsor"))
{
    @await Component.InvokeAsync("NotificationBell")
}
```
Place around the existing line 531 call. This stops the component from ever being invoked for non-Sponsors (matches the existing role-split nav pattern). Keep the bell out of the Admin/Tourist/anonymous experience.

### 2. `NotificationBellViewComponent.cs` — return a valid empty model on both no-data paths
Replace the two `return View("Default");` calls (lines 22 and 30) with:
```csharp
return View("Default", new NotificationBellVM { UnreadCount = 0, SponsorId = 0 });
```
This guarantees the view never renders against a null model, even for Sponsors whose `ApplicationUserId` can't be resolved.

### 3. `Views/Shared/Components/NotificationBell/Default.cshtml` — defensive null-check
Change line 11 to:
```razor
@if (Model?.UnreadCount > 0)
```
Second line of defense: a future null model degrades gracefully (no badge) instead of crashing the page.

## Behavior preserved
- Sponsor signed in with real notifications: `userId > 0` → `View("Default", vm)` with actual `UnreadCount` (unchanged path, line 32-34).
- Sponsor without linked record, Admin, Tourist, anonymous: empty VM / guarded invocation → bell shows no unread badge, page loads.

## Validation
1. Load app logged out (anonymous) → page loads, no NRE.
2. Log in as **Admin** → account-management / admin pages load, no NRE.
3. Log in as **Tourist** → explore/trip pages load, no NRE.
4. Log in as **Sponsor** with seeded notifications → bell renders and shows correct unread count badge.
5. Log in as a Sponsor whose `ApplicationUserId` is nulled (edge case from account-delete flow) → page loads, bell shows no badge, no crash.
6. `dotnet build` → 0 new warnings/errors beyond baseline (~70).
