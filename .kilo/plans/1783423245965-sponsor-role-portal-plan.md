# EGYXPLORE — Sponsor Role + Portal, Admin Account Management

Implementation-ready plan. Build **phase by phase, one migration per phase**, and confirm Phase 0 works (sponsor can sign up → log in → land on portal) before Phase 1.

## Resolved interpretations (flagged items)
- **(a) Reward "redemption rate"** = `Redeemed count ÷ Total Views` (Views come from the `RewardView` log added in Phase 2). Show as a percentage/ratio. Until Phase 2 lands, My Rewards shows the redeemed count only.
- **(b) "Bending mode" status toggle** = `Reward.Status` ∈ {`Active`, `Paused`, `Expired`}. `Paused`/`Expired` hide the reward from tourists (filter them out wherever tourists see rewards).
- **(c) "From which place"** = **Branch**. Add nullable `Redemption.BranchId` (set at redemption time).
- **(d) Place rating dependency** = **MET**. The `Review` entity already exists tied to `Sponsor`. Decision: **add a new `BranchReview` entity** (per‑branch ratings) for the dashboard's "tourist rating for your place"; keep the existing `Sponsor.Review` for the public Near Me page.
- **(e) Account deletion** = **hard delete everything**: delete `ApplicationUser` and the linked `Tourist`/`Sponsor` plus their dependent rows.
- **Move Lat/Long → Branch**: remove `Sponsor.Lat`/`Long`, add `Branch` with `Lat`/`Long`, **backfill one `Branch` per existing sponsor** from its current coords, and **fix Near Me** to use branch coordinates.

## Naming / routing note
- New portal controller = **`SponsorPortalController`** (route `SponsorPortal`), NOT `SponsorController`, to avoid clashing with the existing misspelled admin `SponserController` and the orphaned `asp-controller="Sponsor"` admin nav link. Fix that admin link to `asp-controller="Sponser"` (navigation tweak only).
- Mirror the `Tourist`↔`ApplicationUser` linking for `Sponsor`: add `Sponsor.ApplicationUserId` (FK, nullable, `HasMaxLength(450)`, `NoAction` like Tourist) and `ISponsorRepository.GetOrCreateByApplicationUser(ApplicationUser)` returning the linked/created `Sponsor`.

---

## Phase 0 — Foundation (migration: `SponsorPortalFoundation`)
**Entities / schema**
- `Sponsor`: add `public string? ApplicationUserId { get; set; }` (remove `Lat`/`Long`).
- New `Branch`: `Id, int SponsorId (FK), string Name, string Address, float Lat, float Long, int? ContactNumber`.
- New `RewardBranch` (join): `int RewardId (FK Reward), int BranchId (FK Branch)` — PK = (RewardId, BranchId).

**Model seed changes (`Data/TouristContext.cs`)**
- Remove `Lat`/`Long` from the `Sponsor` `HasData` entries.
- Seed `Branch`: one row per existing sponsor (id = sponsor id, `Name` = `"Main Branch"` or sponsor name, `Address`/`ContactNumber` from the sponsor, **`Lat`/`Long` = the existing seeded coords**: S1 30.0669/31.2243, S2 30.1118/31.4056, S3 30.0444/31.2358, S4 25.6989/32.6394, S5 24.0822/32.8872).
- Seed `RewardBranch`: reward `i` → branch `i` for i=1..5 (preserves current availability).
- Seed the `Sponsor` Identity role (`Id="role-sponsor-id"`, `Name="Sponsor"`, `NormalizedName="SPONSOR"`) alongside the existing Admin/User role seeds.

**Code**
- `ISponsorRepository` / `SponsorRepository`: add `Sponsor GetOrCreateByApplicationUser(ApplicationUser user)` (copy `TouristRepository.GetOrCreateByApplicationUser`, swapping to `Sponsor` and seeding `Name` from the user).
- `RegisterViewModel`: add `string AccountType` (`"Tourist"`/`"Sponsor"`) + conditional Sponsor fields `BusinessName, Type, Address, ContactNumber` (reuse `Sponsor` fields). Keep `UserName/UserEmail/Password/ConfirmPassword`.
- `Views/Account/Register.cshtml`: account‑type radio/toggle at top; when "Sponsor", show Business/Type/Address/Contact fields (still hidden if Tourist).
- `AccountController.Register` (POST): after `CreateAsync`, ensure the `Sponsor` role exists via `RoleManager` (`if (!await roleManager.RoleExistsAsync("Sponsor")) await roleManager.CreateAsync(new IdentityRole("Sponsor"))`); `AddToRoleAsync(..., accountType == "Sponsor" ? "Sponsor" : "User")`; if Sponsor, call `_sponsorRepo.GetOrCreateByApplicationUser(createdUser)` and set `Name=BusinessName, Type, Address, ContactNumber`. Inject `RoleManager<IdentityRole>`, `ISponsorRepository`, `ISponsorRepository` (already have `ITouristRepository`).
- `AccountController.Login` (POST): add `else if (await userManager.IsInRoleAsync(user, "Sponsor")) return RedirectToAction("Index", "SponsorPortal");` before the Tourist fallback.
- `_Layout.cshtml` nav: add a **Sponsor** branch to the role split (`@if (User.IsInRole("Sponsor"))`) with: Home, Dashboard (`SponsorPortal/Index`), Branches, My Rewards, Redemption History, Notifications, Support, Reports. Do **not** show Admin CRUD or Tourist Explore/Trip/Near Me. Also fix the admin "Sponsors" dropdown link to `asp-controller="Sponser"`. Add a notification **bell** + dropdown panel in the Sponsor utility bar (reuse the AI‑fab floating‑panel interaction pattern from `_Layout`; list recent notifications, mark‑as‑read).
- **Fix Near Me** (`NearMeController`): `Sponsor` no longer has `Lat`/`Long`. Compute each sponsor's distance as the **minimum distance from the origin to any of its `Branches`** (Haversine). Update `SponsorCardVM` to carry the nearest‑branch distance (+ branch count). `Details` should list the sponsor's branches (addresses) instead of a single sponsor coordinate. (NearMe already uses `TouristContext` directly, so just `Include(s => s.Branches)`.)

**Validation Phase 0**: `dotnet build` 0 errors; `dotnet ef migrations add SponsorPortalFoundation` → `Up()` contains `CreateTable Branch`, `CreateTable RewardBranch`, `AddColumn Sponsor.ApplicationUserId`, `DropColumn Sponsor.Lat/Long`, and seed `InsertData` for Branch/RewardBranch + Sponsor role; `database update` applies. Register a Sponsor account → log in → lands on `SponsorPortal/Index`. Near Me still lists sponsors with sane "X km" (nearest branch).

---

## Phase 1 — Branches & Reward management (migration: `SponsorBranchesRewards`)
**Schema**: `Reward` add `public string Status { get; set; } = "Active";` (column, default `Active`). (Branch multi‑select uses `RewardBranch` from Phase 0.)
**Controller `SponsorPortalController`** `[Authorize(Roles="Sponsor")]`, resolve current sponsor via `_sponsorRepo.GetOrCreateByApplicationUser(await _userManager.GetUserAsync(User))` in an overridden `OnActionExecuting` (store in `ViewBag.CurrentSponsorId`).
- **Branches**: `Branches()` list cards; `BranchCreate/Edit/Delete` (CRUD on `Branch` where `SponsorId == current`).
- **Reward create**: `RewardCreate()` GET builds VM with the sponsor's `Branch` list (multi‑select `SelectedBranchIds`); POST creates `Reward` + `RewardBranch` rows for each selected branch. Reuse `AddNewRewardVM`-style fields; keep it portal‑scoped (no Sponsor picker).
- **My Rewards** (`MyRewards()`): list sponsor's rewards. Each row: title, type, **redemption count** (`Redemptions.Count`), **views** (Phase 2 — show `0`/pending until Phase 2), **rate** (Phase 2: redeemed÷views), **`Status` badge** (`badge-status-active/paused/expired`), and actions **Edit**, **Remove**, **status toggle** (sets `Active`⇄`Paused`; manual Remove with redemptions ⇒ set `Status="Expired"` soft‑delete; Remove with no redemptions ⇒ hard delete). Edit reuses create VM incl. branch multi‑select.
**Views**: `Branches.cshtml`, `BranchForm.cshtml` (create/edit shared), `RewardForm.cshtml`, `MyRewards.cshtml`. Reuse `.stat-card`, `.filter-bar`, `.badge-status-*` (define inline per view, matching the existing per‑view pattern in `Trip`/`Destination`/`Reward` Indexes).
**Validation**: sponsor can CRUD branches; create a reward assigned to ≥1 branch; My Rewards shows counts + status; Paused reward disappears from tourist‑facing reward views (Near Me Details + reward details — filter `Status == "Active"`).

---

## Phase 2 — Dashboard (migration: `SponsorDashboardTracking`)
**Schema**: 
- `RewardView (Id, int RewardId FK, int? TouristId, DateTime ViewedDate)` — increment when a tourist opens a reward's details page.
- `Redemption` add `public int? BranchId { get; set; }` (FK → `Branch`, nullable).
- `BranchReview (Id, int Rating [Range 1‑5], string? Comment, int TouristId FK, int BranchId FK, DateTime CreatedDate)` — mirrors `Review` but per branch.
**Code**:
- Reward‑view tracking: in the tourist‑visible reward details (reuse existing public `RewardController.Details` — minor additive change, flagged) log a `RewardView` for authenticated tourists (`TouristId` from `_touristRepo.GetOrCreateByApplicationUser`). (This is the only touch to an existing controller; it's additive view‑tracking only.)
- `SponsorPortal/Index` (Dashboard): `.stat-card` summary cards:
  - **Redeemed rewards count** = `Redemptions.Count(r => r.Reward.SponsorId == current)`.
  - **Reward detail views** = `RewardViews.Count(v => v.Reward.SponsorId == current)`.
  - **Most‑wanted reward** = top `Redemption` count; **"from which place"** = the `Branch` (via `Redemption.BranchId`) with the most redemptions for that reward.
  - **Tourist rating for your place** = average of `BranchReview.Rating` across the sponsor's branches (0 if none).
  - **Reward rate** per reward = `Redeemed ÷ Views` (from Phase 1 My Rewards column, now populated).
**Seed** (optional, for smoke test): a few `BranchReview` rows + a `RewardView` or two tied to existing sponsors/branches.
**Validation**: dashboard cards populate with real numbers from seed; rate = redeemed/views computes.

---

## Phase 3 — Redemption History (no new migration)
- `SponsorPortal/RedemptionHistory`: table of `Redemptions` where `Reward.SponsorId == current`: tourist name (`Redemption.Tourist.Name`), reward title, branch (`Redemption.BranchId` → `Branch.Name`), date, voucher `Code`, `Status` (`badge-status-*`).
- Filters (`.filter-bar` pattern): date range (from/to), by reward (select), by status (select). Server‑side filtering.
**Validation**: table lists seeded redemptions with branch + tourist; filters work.

---

## Phase 4 — Notifications (migration: `SponsorNotifications`)
**Schema**: `Notification (Id, int SponsorId FK, string Message, string Type, DateTime CreatedDate, bool IsRead)`.
**Generation (lazy, no background job)**: in `SponsorPortalController.OnActionExecuting` (or a small `NotificationService`), for the current sponsor, scan once per load and create notifications for: a reward redeemed since last check (optional — simpler: on redemption creation, but none exists yet, so generate from recent redemptions), `Reward.ExpirationDate < today` (type `Expired`), `Reward.QuantityAvailable == 0` (type `OutOfStock`). Guard against duplicate notifications (only create if none exists for that reward+type recently).
**UI**: bell in Sponsor utility bar (from Phase 0) opens dropdown listing recent notifications with **mark‑as‑read** (POST `MarkRead(id)`) and an "all read" action. Reuse AI‑fab floating‑panel styling/pattern.
**Validation**: expiring/out‑of‑stock seeded rewards produce notifications on portal load; mark‑as‑read clears the unread state.

---

## Phase 5 — Support (migration: `SponsorSupport`)
**Schema**: `SupportTicket (Id, int SponsorId FK, string Subject, string Message, string Status="Open", DateTime CreatedDate)`.
**UI**: `SponsorPortal/Support` GET shows a form (subject + message) and a list of the sponsor's tickets with `Status` (`Open`/`Resolved`). POST creates a ticket for the current sponsor. Admin‑side inbox is **out of scope** (flagged; fast follow‑up).
**Validation**: sponsor submits a ticket → appears in their list with `Open`.

---

## Phase 6 — Reports (no new migration)
- `SponsorPortal/Reports`: aggregation over the sponsor's own `Redemptions`/`Rewards`/`RewardViews`, grouped **by month** (table: month, redemptions, reward views, top reward). Add a **Chart.js** chart via CDN (no new build tooling) — e.g., monthly redemptions line/bar. Reuse `.stat-card` for headline numbers.
**Validation**: monthly table + chart render from seed data.

---

## Admin Account Management (replaces `Role/Create` + `Role/AssignRole` split)
- New **`AccountController` (+ views) `AccountManagement`** `[Authorize(Roles="Admin")]`: list all `ApplicationUser`s with their role(s). Per row: **change‑role** dropdown (Tourist/User/Sponsor/Admin) → POST reuses `RoleManager` (remove all current roles, add selected — already role‑agnostic, so Sponsor works) and **delete** with confirmation.
- **Delete** = hard delete: `UserManager.DeleteAsync(user)` **plus** delete the linked `Tourist` and/or `Sponsor` and their dependents (ordered to satisfy FKs: e.g., for a Sponsor delete its `RewardBranch`, `BranchReview`, `Reward`→`Redemption`, `Notification`, `SupportTicket`, then `Branch`, then `Sponsor`; for a Tourist delete `Redemption`, `UserMission`, `TripPlan`→`TripDestination`, `Review`, `BranchReview`, then `Tourist`). User explicitly chose hard delete everything (accepts broken Redemption/TripPlan history).
- Keep existing `RoleController` (or fold its logic in); the new page is the convenient surface. Don't touch other Admin CRUD.
**Validation**: admin lists accounts, changes a user's role, deletes an account (login + domain data gone).

---

## Cross‑cutting / constraints
- One focused migration per phase (names above); keep unrelated tables out of each.
- Reuse `.stat-card`, `.badge-status-*`, `.filter-bar`, existing color/font tokens, the `ApplicationUserId` pattern, and the AI‑fab floating‑panel interaction for notifications.
- Don't alter Tourist‑facing or Admin CRUD pages beyond the navigation + account‑management changes described.
- After each phase: `dotnet build` (0 errors) + apply migration + smoke test the new route(s).

## Open follow‑ups (flagged, not built)
- Admin inbox view for `SupportTicket` (Phase 5) — capture‑only for now.
- Decide exact label wording for `Paused` vs `Expired` in tourist‑facing copy.
- Reward‑view tracking currently hooks the admin `RewardController.Details`; if a dedicated tourist reward page is later built, move tracking there.
