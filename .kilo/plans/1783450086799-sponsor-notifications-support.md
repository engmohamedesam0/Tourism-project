# Sponsor Portal — Part 4: Notifications & Support

## Context
Builds on Parts 1–3: Sponsor role/portal, `Reward.Status`, `Redemption.BranchId`, `RewardView`, sponsor dashboard & redemption history. Current state confirmed:
- `Notification` / `SupportTicket` do **not** exist yet.
- Redemptions are only **seeded**, never created via code — so the "reward redeemed" event must be detected by a **lazy scan** (the mechanism the task specifies), not a creation hook.
- The existing "floating AI-icon panel" is a Bootstrap modal opened from a fixed FAB button, styled with `.ai-fab` / `.ai-modal*` classes in `Views/Shared/_Layout.cshtml` (lines ~479–514, styling ~522–601). Reuse this open/close-panel + list pattern.
- Sponsor pages span several controllers (`SponsorPortal`, `SponsorReward`, `SponsorBranch`, `SponsorRedemption`), all rendered through `_Layout.cshtml`.

## Entities (one migration: `SponsorNotificationsAndSupport`)
`Models/Notification.cs` (strict 6-field spec — **no** extra columns; dedupe via message content):
```
Id, SponsorId (FK->Sponsor), Message, Type ("Redeemed"|"Expired"|"SoldOut"),
CreatedDate, IsRead
```
- Dedup rule to keep lazy generation idempotent (no spam): a notification already exists iff there is a row with the same `SponsorId` + `Type` + `Message`. Message strings are made unique per source:
  - Redeemed: `"Reward '<title>' was redeemed (code <Redemption.Code>)."`
  - Expired:  `"Reward '<title>' has expired."`
  - SoldOut:  `"Reward '<title>' is sold out."`
  (Optional future improvement: add nullable `ReferenceId` to link to Reward/Redemption — not required now.)

`Models/SupportTicket.cs`:
```
Id, SponsorId (FK->Sponsor), Subject, Message, Status ("Open"|"Resolved"), CreatedDate
```

Add `DbSet<Notification>` and `DbSet<SupportTicket>` to `TouristContext`.

## Notification generation (lazy, on portal load)
New scoped `Services/NotificationService.cs` (`INotificationService`):
- `EnsureForSponsor(int sponsorId)`:
  1. **Redeemed** — for each `Redemption` whose `Reward.SponsorId == sponsorId` and no matching Notification exists → create one.
  2. **Expired** — for each of the sponsor's `Reward`s with `ExpirationDate < DateTime.Now` and no matching Notification → create one.
  3. **SoldOut** — for each of the sponsor's `Reward`s with `QuantityAvailable == 0` and no matching Notification → create one.
  - All inserts scoped to `SponsorId`; save once.
- `GetForSponsor(int sponsorId)` → list (newest first) + unread count.

## UI: bell + dropdown in portal nav (reuse AI-panel pattern)
- New `ViewComponents/NotificationBellViewComponent.cs`:
  - Guard `@if (User.IsInRole("Sponsor"))` (returns empty otherwise).
  - Resolves current Sponsor via `UserManager` + `ISponsorRepository` (same pattern as `SponsorPortalController`).
  - Calls `EnsureForSponsor(...)` (this is the "checked lazily on portal page load").
  - Renders `Components/NotificationBell/Default.cshtml`: a bell button (mirror `.ai-fab` styling, positioned top-right so it doesn't collide with the bottom-right AI FAB) with an **unread-count badge**; clicking opens a Bootstrap **modal** mirroring `.ai-modal` (header/body/footer) listing notifications (read = muted, unread = highlighted), each with a **Mark read** action, plus **Mark all read**.
- Inject in `_Layout.cshtml` next to the AI FAB: `@await Component.InvokeAsync("NotificationBell")`. This makes the bell appear on **every** sponsor page and runs generation once per load (DRY — no per-page edits).
- `MarkRead` / `MarkAllRead`: `[HttpPost]` actions in new `SponsorNotificationController` (scoped to sponsor ownership — 404 if notification isn't the sponsor's). Invoked from the dropdown via a small `fetch` POST; on success update the item/badge in the DOM (avoid full reload / modal close). Fallback: redirect to `Request.Headers["Referer"]`.

## Support page
New `SponsorSupportController` (`[Authorize(Roles="Sponsor")]`):
- `Index()` — list the sponsor's own tickets (`SponsorId` filter) with `Status` badge; link to create.
- `Create()` GET/POST — subject + message form; on POST set `SponsorId`, `Status="Open"`, `CreatedDate=Now`, save, redirect to `Index`.
- View `Views/SponsorSupport/Index.cshtml` (table of own tickets, status badges) and `Create.cshtml` (reuse `.filter-bar`/form styling).
- Add nav entry: a card in `SponsorPortal/Index.cshtml` (like the Dashboard/History cards added in Part 3) and/or a modal link in the NotificationBell area.
- **Admin inbox is OUT OF SCOPE.** Flag as recommended quick follow-up (read-only Admin list of tickets + Resolve action) — not built unless confirmed.

## Files / entities changed
- `Models/Notification.cs` (new), `Models/SupportTicket.cs` (new)
- `Data/TouristContext.cs` (DbSets)
- `Services/NotificationService.cs` + `INotificationService.cs` (new)
- `ViewComponents/NotificationBellViewComponent.cs` + `Views/Shared/Components/NotificationBell/Default.cshtml` (new)
- `Views/Shared/_Layout.cshtml` (invoke component for Sponsor role)
- `Controllers/SponsorNotificationController.cs` (new: MarkRead/MarkAllRead)
- `Controllers/SponsorSupportController.cs` (new)
- `Views/SponsorSupport/Index.cshtml`, `Create.cshtml` (new)
- `Views/SponsorPortal/Index.cshtml` (Support nav card)
- `Program.cs` (register `INotificationService` scoped)
- Migration `SponsorNotificationsAndSupport.cs` (+ Designer)

## Constraints honored
- Everything filtered by signed-in `SponsorId` (queries, MarkRead ownership check, ticket creation).
- One focused migration for `Notification` + `SupportTicket`.
- Reuses `.ai-fab`/`.ai-modal` panel pattern and existing badge/table/form styling — no new visual language.

## Validation
1. Seed/insert a `Redemption` for a sponsor's reward → load any sponsor page → bell shows new "redeemed" notification → Mark read clears it & decrements badge.
2. Set a sponsor reward's `ExpirationDate` in the past (or `QuantityAvailable=0`) → next load generates Expired / SoldOut notification exactly once (reload doesn't duplicate).
3. Submit a Support ticket → appears in sponsor's own `Index` with `Status = Open`; another sponsor cannot see it.
4. Build succeeds (`dotnet build`); run `dotnet ef migrations add SponsorNotificationsAndSupport`.

## Open questions / flags
- Admin-side ticket inbox intentionally omitted (per scope). Recommend adding a minimal Admin list + "Resolve" as a follow-up.
- No redemption-creation endpoint exists, so "redeemed" notifications rely on the lazy scan detecting `Redemption` rows — consistent with the specified lazy approach.
- `Notification` kept to the strict 6-field spec; dedupe is message-based. If cleaner linking is wanted later, add nullable `ReferenceId`.
