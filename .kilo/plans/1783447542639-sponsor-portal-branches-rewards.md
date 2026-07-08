# Sponsor Portal — Part 2: Branches page & Reward management

## Context (post Part 1)
- `Sponsor.ApplicationUserId` links the login to the sponsor record (mirrors `Tourist`).
- `Branch` exists: `Id, SponsorId, Name, Address, Lat, Long, ContactNumber?` (+ `RewardBranches` nav).
- `RewardBranch` join exists: composite key `(RewardId, BranchId)`.
- `Reward` currently has NO `Status` field. `Redemption.RewardId` is a required FK (convention → cascade delete), so **hard-deleting a Reward would delete its `Redemption` history** → must soft-delete.
- `SponsorPortalController` (`[Authorize(Roles="Sponsor")]`) already resolves the current sponsor via `ApplicationUserId`.
- Pattern reference for reuse: `RewardController` (Admin CRUD), `Reward/Create.cshtml`, `Reward/Edit.cshtml`, `AddNewRewardVM`, `NearMeController` (uses `TouristContext` directly for join writes).

## Confirmed interpretations (working assumptions from prompt)
- **Redemption rate** = `Redemptions.Count` ÷ `QuantityAvailable` (count of redemption records tied to the reward; NOT `PointsRedeemed`). Displayed as e.g. `40% (2/5)`.
- **Status toggle / "bending mode"** = `Paused` (On-hold): a stored state that temporarily hides the reward from tourists. Implemented as a `Status` string with values `Active | Paused | Expired | Removed`.
- **Remove** = soft-delete → set `Status = "Removed"` (never hard-delete).
- **Flag (open item):** Actually hiding `Paused`/`Removed` rewards from tourists requires touching tourist-facing redemption views, which Part 2 explicitly forbids. So in Part 2 we only **store** the status; the tourist-side filtering is deferred to a later part. Note this in the deliverable.

## Schema change (one isolated migration)
Add `Reward.Status` (non-nullable `nvarchar`, **default `'Active'`**).
- Update `Models/Reward.cs`: add `public string Status { get; set; } = "Active";`.
- In `TouristContext` `OnModelCreating`, set the column default (`HasDefaultValueSql("'Active'")` or `defaultValue`) so existing + seeded rows become `'Active'`.
- Add `Status = "Active"` to the 5 seeded `Rewards` in `HasData` (keeps seed complete; migration emits `UpdateData`).
- Migration name: `SponsorPortalRewardsStatus`. No other schema changes (Branch/RewardBranch already exist).

## Repositories
- New `Models`/`Repositories`: `IBranchRepository : IRepository<Branch>` + `BranchRepository` with:
  - `IEnumerable<Branch> GetBySponsorId(int sponsorId)` (filter by `SponsorId`).
  - `Branch? GetByIdWithRewardBranches(int id)` (optional, for edit).
- Extend `IRewardRepository`/`RewardRepository`:
  - `IEnumerable<Reward> GetBySponsorId(int sponsorId, bool includeRemoved = false)` — base query filters `SponsorId`; exclude `Status == "Removed"` unless requested.
  - `Reward? GetByIdWithDetails(int id)` — `Include(r => r.Redemptions).Include(r => r.RewardBranches).ThenInclude(rb => rb.Branch)`.
- Register `IBranchRepository`/`BranchRepository` in `Program.cs` (mirror existing `ISponsorRepository` registration).
- `SponsorRewardController` also injects `TouristContext` to write/clean `RewardBranch` join rows (same pattern as `NearMeController`).

## ViewModels
- `View Model/BranchViewModel.cs` (new): `Id, Name, Address, Lat (float), Long (float), ContactNumber (int?)`.
- Extend `AddNewRewardVM` (or new `SponsorRewardViewModel`) with:
  - `List<int> SelectedBranchIds`
  - `List<Branch> AvailableBranches` (only the sponsor's own branches).
  - Recommended: keep these in a **new** `SponsorRewardViewModel` to avoid coupling with Admin `AddNewRewardVM` (which carries `Sponsors`/`SponsorId` the sponsor UI doesn't need).

## Controllers (both `[Authorize(Roles = "Sponsor")]`)
Shared helper in each: resolve current `Sponsor` by `ApplicationUserId`; if null → `NotFound()`/message.

### `SponsorBranchController`
- `Index()` → list current sponsor's branches (`GetBySponsorId`) as cards (reuse card/badge styling).
- `Create()` GET/POST → add `Branch` with `SponsorId = currentSponsor.Id`.
- `Edit(int id)` GET/POST → load branch; **verify `branch.SponsorId == currentSponsor.Id`** else `Forbid()`/`NotFound()`; update.
- `Delete(int id)` GET/POST → verify ownership; hard-delete (Branch→RewardBranch is cascade, so join rows are cleaned; Rewards themselves are untouched).

### `SponsorRewardController`
- `Index()` ("My Rewards") → `GetBySponsorId(id, includeRemoved:false)` with `Redemptions` included; pass `IEnumerable<Reward>` to view. View computes redemption rate per row and shows a **Status badge** + **Edit**, **Pause/Resume** toggle, **Remove** actions.
- `Create()` GET → build VM with `AvailableBranches = currentSponsor.Branches`; POST → create `Reward` (`SponsorId = currentSponsor.Id`, `Status = "Active"`), then sync `RewardBranch` rows for `SelectedBranchIds` (each validated to belong to this sponsor).
- `Edit(int id)` GET/POST → load via `GetByIdWithDetails`; verify `SponsorId`; update scalar fields; re-sync `SelectedBranchIds`.
- `TogglePause(int id)` POST → verify ownership; flip `Status` between `Active` ↔ `Paused`; save; redirect to `Index`.
- `Remove(int id)` POST (or `DeleteConfirmed`) → verify ownership; **set `Status = "Removed"`** (soft-delete), do NOT call repo delete; redirect to `Index`.
- **Join sync helper** (private): given `rewardId` + `selectedBranchIds`, remove existing `RewardBranch` rows for that reward from `TouristContext`, then `Add` new ones (only for branch ids owned by the sponsor). `SaveChanges()`.

### RewardBranch sync detail
```
var existing = _context.RewardBranches.Where(rb => rb.RewardId == rewardId);
_context.RewardBranches.RemoveRange(existing);
foreach (var bid in selectedBranchIds.Where(id => ownedBranchIds.Contains(id)))
    _context.RewardBranches.Add(new RewardBranch { RewardId = rewardId, BranchId = bid });
_context.SaveChanges();
```

## Views (reuse existing card/table/badge/filter-bar styling)
- `Views/SponsorBranch/Index.cshtml` — branch cards + "Add Branch" button; each card has Edit/Delete.
- `Views/SponsorBranch/Create.cshtml`, `Edit.cshtml`, `Delete.cshtml` — form for `Name, Address, Lat, Long, ContactNumber`; mirror `Reward/Create.cshtml` styling; `@section Scripts` validation partial.
- `Views/SponsorReward/Index.cshtml` — table/cards of rewards; columns: Title, Type, Qty, **Redemption rate** (`@($"{Math.Round((double)r.Redemptions.Count / r.QuantityAvailable * 100)}% ({r.Redemptions.Count}/{r.QuantityAvailable})")` guarding divide-by-zero), Status badge, actions.
- `Views/SponsorReward/Create.cshtml`, `Edit.cshtml` — reward fields + **multi-select** for branches:
  `<select asp-for="SelectedBranchIds" asp-items="branchOptions" multiple class="form-control">` (build `SelectList` from `AvailableBranches`).
- `Views/SponsorReward/Delete.cshtml` — confirm soft-remove.

## Nav (`Views/Shared/_Layout.cshtml`, sponsor block ~line 356)
Add inside the `else if (User.IsInRole("Sponsor"))` block, after Home:
```
<li><a class="nav-link" asp-controller="SponsorBranch" asp-action="Index"><i class="bi bi-geo-alt-fill"></i> Branches</a></li>
<li><a class="nav-link" asp-controller="SponsorReward" asp-action="Index"><i class="bi bi-gift-fill"></i> My Rewards</a></li>
```

## Scope/security enforcement (per constraint)
- Every read/write is filtered by the resolved `SponsorId` at the query level.
- Edit/Delete/Toggle/Remove re-load the entity and assert ownership before mutating; otherwise `Forbid()`/`NotFound()`.
- Multi-select branch ids are re-validated against the sponsor's own branch list before writing `RewardBranch`.

## Deliverable / validation (end-to-end, like Part 1)
1. Build (`dotnet build`) → 0 errors.
2. `dotnet ef migrations add SponsorPortalRewardsStatus` then `dotnet ef database update` (real migration, no raw SQL).
3. Run app; as a sponsor (e.g. the `testsponsor03` account from Part 1 or a fresh one):
   - Create a Branch → appears in Branches list (and only for that sponsor).
   - Create a Reward, select that branch in the multi-select → reward saved with a `RewardBranch` row.
   - On "My Rewards": redemption rate shows `0% (0/N)`; Edit updates fields + branch selection; **Pause** sets `Status = Paused` (badge updates); **Remove** sets `Status = Removed` and the reward disappears from the list but its `Redemption` rows (if any) remain in DB.
   - Confirm a sponsor cannot see/edit another sponsor's branches or rewards (ownership guard).
   - Verify via DB: `SELECT Status FROM Rewards`, `SELECT * FROM RewardBranches`.
4. State the interpretations used (redemption rate, Paused = bend mode) and the open item (tourist-side hiding deferred).

## Files/entities touched
- `Models/Reward.cs` (add `Status`)
- `Data/TouristContext.cs` (Status default + seed `Status="Active"`)
- `Repositories/IBranchRepository.cs` (new), `Repositories/BranchRepository.cs` (new)
- `Repositories/IRewardRepository.cs`, `Repositories/RewardRepository.cs` (sponsor-scoped queries)
- `Program.cs` (register `IBranchRepository`)
- `View Model/BranchViewModel.cs` (new), `View Model/SponsorRewardViewModel.cs` (new) — or extend `AddNewRewardVM`
- `Controllers/SponsorBranchController.cs` (new), `Controllers/SponsorRewardController.cs` (new)
- `Views/SponsorBranch/*` (new), `Views/SponsorReward/*` (new)
- `Views/Shared/_Layout.cshtml` (sponsor nav links)
- Migration: `SponsorPortalRewardsStatus`
