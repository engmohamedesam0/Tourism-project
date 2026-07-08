# Part 5: Sponsor Reports + Admin Account Management

## Current State

- `RoleController.AssignRole` already removes all current roles and adds the new one (role-name-agnostic). `Sponsor` role is seeded in `TouristContext`. AssignRole works with it, but no one has verified the end-to-end flow including orphaned `Sponsor.ApplicationUserId` edge cases insofar as routing to Sponsor pages.
- `Redemption.RedemptionDate`, `RewardView.ViewedDate`, and `Reward.SponsorId` provide all data needed for monthly aggregation — no schema changes.
- No Chart.js CDN reference anywhere yet.
- Admin nav has: Tourists, Destinations, Trip Plans, Missions, Rewards (dropdown), Near Me, Features. No Accounts/Users page.
- `Role/Create` and `Role/AssignRole` exist as basic scaffolded pages.

---

## Task 1: Sponsor Reports Page

### 1.1 Controller
- Add `public IActionResult Reports()` to `SponsorPortalController`.
- Resolve `sponsorId` using `GetUserAsync(User)` pattern (already in that controller).
- Build `ReportsVM` with:
  - `MonthlyRedemptionRows`: list of `{ Year, Month, RedemptionCount, PointsRedeemed }`
  - `MonthlyViewRows`: list of `{ Year, Month, ViewCount }`
  - `TopRewards`: list of `{ RewardTitle, RedemptionCount, ViewCount }` scoped to this sponsor
  - `CurrentSponsorName`
- Queries should use LINQ `.GroupBy` on `RedemptionDate.Year` / `.Month` from `_context.Redemptions` and `_context.RewardViews` joined/filtered through `Reward.SponsorId == sponsorId`.
- Use existing `ResolveCurrentSponsor()` approach (or inline it) — not `FindFirst`. Consistent with the controller's existing pattern.

### 1.2 View Model
- Create `View Model/SponsorReportsVM.cs` with:
  - `string CurrentSponsorName`
  - `List<MonthlyStatRow> ReportRows` — each `{ int Year, int Month, string MonthLabel, int Redemptions, int PointsRedeemed, int Views }`
  - `List<TopRewardRow> TopRewards` — each `{ string RewardTitle, int Redemptions, int Views }`

### 1.3 View
- Create `Views/SponsorPortal/Reports.cshtml`
- Layout: two sections side-by-side or stacked:
  1. **Monthly breakdown table** — columns: Month, Redemptions, Points Redeemed, Reward Views
  2. **Top rewards table** — columns: Reward, Redemptions, Views
- Include a **Chart.js bar chart** (CDN) showing redemptions per month for the current year. Load Chart.js in the page `<script src="https://cdn.jsdelivr.net/npm/chart.js">` inside the view's section.
- Reuse existing `.destinations-table`, `.page-title`, `.card`, `.border-0.shadow-sm.rounded-4` patterns.
- Add "Reports" nav link to sponsor section in `_Layout.cshtml`.
- Add a Reports card to `SponsorPortal/Index.cshtml`.

### 1.4 Navigation
- Add `<li>` for Reports in the Sponsor nav block of `_Layout.cshtml`, placed between Rewards and Alerts (matches card order on Index page).

---

## Task 2: Admin Account Management

### 2.1 Verify Sponsors role works end-to-end
- Confirm `RoleController.AssignRole` POST works: it calls `RemoveFromRolesAsync` then `AddToRoleAsync`. Since `Sponsor` is seeded as an `IdentityRole`, this path is valid.
- Edge case: Admin assigns a user to Sponsor role when that user has no `Sponsor` record linked. The sponsor login will hit `Challenge()` on every page. This is acceptable intentional behavior — an Admin must create the Sponsor record separately.
- Edge case: Admin removes Sponsor role from a user that has a linked `Sponsor` record. The `Sponsor.ApplicationUserId` stays populated, the domain record is preserved, but the login can no longer access Sponsor pages because `User.IsInRole("Sponsor")` is false. This is correct.

### 2.2 Add delete account action
- Add `[HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> Delete(string id)` to `RoleController`.
- `id` is the `ApplicationUser.Id` string.
- Guard: if `id == currentAdminUserId`, reject with `ModelState.AddModelError` + warning message.
- Find user via `userManager.FindByIdAsync(id)`. If null, return NotFound.
- **Non-destructive delete**: before calling `DeleteAsync`, set `user.SponsorId` or null out linked entity `ApplicationUserId` fields so historical data stays intact.
  - Use `_context` (TouristContext) injected into RoleController to null out `Tourist.ApplicationUserId` and `Sponsor.ApplicationUserId` for that user, then `SaveChangesAsync()`.
  - Then call `await userManager.DeleteAsync(user)`.
- Return `RedirectToAction("ManageAccounts")` (new unified page) — not back to AssignRole.

### 2.3 Unified Admin Manage Accounts page
- Add `[HttpGet] public async Task<IActionResult> ManageAccounts()` to `RoleController`.
- Model: `List<AccountRow>` where `AccountRow = { string UserId, string UserName, string Email, string CurrentRole, bool IsCurrentAdmin }`.
- Build by iterating `userManager.Users`, calling `GetRolesAsync` for each.
- Add `[HttpPost] public async Task<IActionResult> ManageAccounts(UserRoleViewModel model)` that reuses the same role-swap logic as existing `AssignRole` POST (remove all, add new). Reuse model — it already has `UserId` and `RoleName`.
- View `Views/Role/ManageAccounts.cshtml`:
  - Page title: "Account Management"
  - Table: User, Email, Current Role (badge), Actions (role dropdown + change button, delete button).
  - Self-demotion/self-delete blocks: if `IsCurrentAdmin`, show badge "You" and disable/hide role dropdown and delete button with tooltip "Cannot change or delete your own account".
  - Delete form per row: POST to `Delete`, hidden anti-forgery, confirmation triggered via `confirm()` dialog with explicit text: "This will permanently remove the login account for userName@email.com. The linked Tourist/Sponsor profile and all historical data will be preserved. Are you sure?"
  - Role change per row: dropdown + submit, reusing same `UserRoleViewModel` fields.

### 2.4 Keep old pages but hide from nav
- Leave `Role/Create` and `Role/AssignRole` views/actions in place. They continue to work if accessed directly.
- Remove their links from the layout (or move them into a non-visible spot). The new `ManageAccounts` replaces the entry point.
- Add ManageAccounts to Admin nav in `_Layout.cshtml`.

---

## Files to Change

| File | Change |
|---|---|
| `View Model/SponsorReportsVM.cs` | New: monthly + top-rewards VMs |
| `Controllers/SponsorPortalController.cs` | Add `Reports()` action |
| `Views/SponsorPortal/Reports.cshtml` | New: table + Chart.js bar chart |
| `Views/Shared/_Layout.cshtml` | Add Reports to Sponsor nav, ManageAccounts to Admin nav |
| `Views/SponsorPortal/Index.cshtml` | Add Reports card |
| `Controllers/RoleController.cs` | Add `ManageAccounts()`, `ManageAccounts(POST)`, `Delete()` |
| `View Model/AccountRow.cs` | New: per-account display model |
| `Views/Role/ManageAccounts.cshtml` | New: unified account table with role change + delete |

---

## Validation Plan

1. **Reports renders correctly**: Run app, log in as Sponsor (e.g. Cairo Marriott sponsor account), navigate to Reports. Verify monthly table has data matching existing seeded redemptions/reward views; chart renders without JS errors.
2. **Sponsor role change works**: In Admin → Manage Accounts, change a Tourist user to Sponsor. They can login (if Sponsor record linked via `ApplicationUserId`) or hit `Challenge()` if not linked. Change back to Tourist — works.
3. **Account delete preserves domain data**: Delete a Tourist user account. Verify `Tourist.ApplicationUserId` is nulled, redemptions/reviews/trip plans still reference the Tourist `Id` and remain queryable.
4. **Admin self-protection**: Admin cannot demote or delete own account. Confirm disabled UI + server-side ModelState check.
5. **Build**: `dotnet build` clean, no new warnings beyond existing baseline.

---

## Decisions (resolved)

- **Q1 — Reports date range**: Default to current calendar year only, showing all 12 months. No year switcher in initial scope — keeps chart readable and implementation minimal.
- **Q2 — Sponsor resolution pattern in new Reports action**: Use `GetUserAsync(User)` to stay consistent with the rest of `SponsorPortalController`.
- **Q3 — Reports navigation exposure**: Add both a top-nav `<li>` in `_Layout.cshtml` and a quick-access card on `SponsorPortal/Index.cshtml`, matching how Dashboard, Redemption History, and Alerts are exposed.
