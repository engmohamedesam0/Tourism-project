# EGYXPLORE — Implementation Plan

## 0. Current State (verified)

### Task A — Features in Admin nav
- `_Layout.cshtml` Admin dropdown (lines 387–432): Tourists, Destinations, TripPlans, Missions, Rewards, Sponsors, divider, Accounts, SupportInbox, Approvals.
- **No Features link exists in the Admin menu.** Features links are only in Tourist nav (line 486) and Guest nav (line 525).
- **Verdict: Task A is already satisfied. No controller/view/route deletion needed.**

### Task B — Notification FAB
Existing implementation:
- `NotificationFab` (FAB + slide-out panel) is already in `_Layout.cshtml` line 813, sponsor-only.
- Old `NotificationBell` (navbar dropdown) component exists but is **never invoked** in the layout.
- Old `ViewComponents/Default.cshtml` (Bootstrap modal bell) exists but is **never invoked** anywhere.
- Font Awesome 6.3.0 is still loaded in `<head>` and used for: `fa-bars` (toggler), and 10 icons in `Views/Home/Index.cshtml`.
- The `.notification-fab` / `.notification-widget-panel` CSS is separate from the AI widget CSS despite being functionally identical.

---

## 1. Task A — Remove Features from Admin account

**Status: Already complete.** No code changes required.

**Validation step:** Grep `Features` across all `Views/Admin*` and `Views/Role/*.cshtml` files. Confirm zero nav links to `FeaturesController` in admin-only views. If any are found, remove them. Do **not** delete `FeaturesController`, `Views/Features/`, or the route — tourists and guests still need the public page.

---

## 2. Task B — Notifications: FAB pattern (replace any bell-in-navbar pattern)

### B1. Delete dead notification code (old bell-in-navbar / modal patterns)
These files are unused and represent the pattern the design doc explicitly rejects:

| File | Action |
|---|---|
| `Views/Shared/Components/NotificationBell/Default.cshtml` | Delete |
| `ViewComponents/NotificationBellViewComponent.cs` | Delete |
| `ViewComponents/Default.cshtml` | Delete |

### B2. Generalize `NotificationFabViewComponent` to all authenticated users
**File:** `ViewComponents/NotificationFabViewComponent.cs`

Current logic returns `Content(string.Empty)` for non-sponsors. Change to:

1. Remove the `User.IsInRole("Sponsor")` early-return guard.
2. Resolve the current user’s entity ID across roles:
   - **Sponsor:** use `ISponsorRepository` to get `SponsorId`, call `_notificationService.GetUnreadCount(sponsorId)`.
   - **Tourist / Admin / other authenticated:** return `UnreadCount = 0` (no notification backend exists yet for these roles, so the FAB shows with no badge and the panel renders the empty state).
3. Pass the role name into the view model so the panel JS knows which endpoint to call (or render a no-data empty state).

**File:** `View_Model/NotificationBellVM.cs`
- Add `string? UserRole { get; set; }` so the view/JS can branch behavior.

### B3. Update `_Layout.cshtml` notification FAB + panel CSS
**File:** `Views/Shared/_Layout.cshtml` (inline `<style>` block, lines 877–1176)

1. **Unify FAB base styles:** Extract shared fixed-position, circle, shadow, and transition properties into a common `.fab-base` class, then have `.ai-fab` and `.notification-fab` extend it. This guarantees both buttons use the exact same `transition: all .25s ease`, hover lift, and shadow behavior.
2. **Verify FAB stack spacing:**
   - AI FAB: `bottom: 22px`, `height: 58px` → top edge at `80px` from viewport bottom.
   - Notification FAB target: `bottom: 92px` (14px gap above AI FAB top edge). Currently set to `94px` (14px gap) — this is correct. Keep it.
   - Mobile media query: AI FAB `bottom: 14px`, `height: 52px` → top edge at `66px`. Notification FAB `bottom: 78px` (12px gap). Update from current `80px` to `78px`.
3. **Badge:** Already uses `--egy-danger`. Keep it. Ensure the badge dot/number appears only when `UnreadCount > 0`.
4. **Panel transitions:** Ensure `.notification-widget-panel` uses the same `transform: translateY(12px) scale(0.96)` → `translateY(0) scale(1)` and `transition: all .25s ease` as `.ai-widget-panel`.

### B4. Update notification panel content (`_NotificationPanel.cshtml`) to match Section 6 spec
**File:** `Views/SponsorNotification/_NotificationPanel.cshtml`

1. Replace Bootstrap `list-group` with custom rows:
   - Each row: small circular icon in tinted background (`--egy-primary` at 8% opacity) on the left.
   - Message text (Nunito, `0.875rem`).
   - Relative timestamp (e.g., "2h ago") in muted small text below the message.
   - Unread rows: `background-color: rgba(200, 131, 42, 0.05)` + `3px solid var(--egy-primary)` left border.
   - Read rows: `opacity: 0.7`.
2. Replace hardcoded `@n.CreatedDate.ToString("MMM dd, HH:mm")` with a relative-time helper (or inline JS that converts ISO dates to "2h ago").
3. Replace `badge bg-danger/bg-warning/bg-info` type tags with a simpler icon-in-tint pattern (the type is communicated by the icon background, not a colored badge).
4. Update the empty state to match Section 6: friendly on-brand text ("You're all caught up 🏺") with a muted icon, no Bootstrap spinner.

### B5. Remove Font Awesome; migrate to Bootstrap Icons
**Files:** `Views/Shared/_Layout.cshtml`, `Views/Home/Index.cshtml`

1. Delete the Font Awesome `<script>` tag from `_Layout.cshtml` line 12.
2. Replace remaining `fa-*` / `fas-*` classes with `bi-*` equivalents:

| Location | Current | Replacement |
|---|---|---|
| `_Layout.cshtml` line 359 (toggler) | `fas fa-bars` | `bi-list` |
| `Home/Index.cshtml` line 16 | `fas fa-map-marker-alt` | `bi-geo-alt-fill` |
| `Home/Index.cshtml` line 112 | `fas fa-compass` | `bi-compass-fill` |
| `Home/Index.cshtml` line 130 | `fas fa-robot` | `bi-stars` |
| `Home/Index.cshtml` line 137 | `fas fa-plane` | `bi-airplane` |
| `Home/Index.cshtml` line 157 | `fas fa-trophy` | `bi-trophy-fill` |
| `Home/Index.cshtml` line 164 | `fas fa-star` | `bi-star-fill` |
| `Home/Index.cshtml` line 183 | `fas fa-map-marked-alt` | `bi-map-fill` |
| `Home/Index.cshtml` line 196 | `fas fa-robot` | `bi-stars` |
| `Home/Index.cshtml` line 209 | `fas fa-trophy` | `bi-trophy-fill` |
| `Home/Index.cshtml` line 222 | `fab fa-twitter` | `bi-twitter` |
| `Home/Index.cshtml` line 223 | `fab fa-facebook-f` | `bi-facebook` |
| `Home/Index.cshtml` line 224 | `fab fa-github` | `bi-github` |

3. Grep the entire `Views/` tree for any remaining `fa-` or `fas-` or `fab-` classes. Fix any stragglers.

---

## 3. Out of Scope (deferred to later passes)

- Full notification backend for tourists/admins (requires `Notification` model + `INotificationService` refactor to be role-agnostic). For now, non-sponsors see an empty-state panel.
- Sections 1–8 design-system refinements (neutral ramp, button variants, radius/shadow tokens, typography scale, card standards, empty-state template, etc.) — these are the design direction, not the two concrete tasks.
- Profile page side panels, Login split-screen, Reviews carousel expansion, Features page zig-zag layout, Toast/snackbar system.

---

## 4. Validation

1. Run the app and log in as **Sponsor** → confirm notification FAB appears with correct unread badge, opens slide-out panel, loads notifications from `/SponsorNotification/Panel`, mark-as-read works.
2. Log in as **Tourist** → confirm notification FAB appears (no badge), opens panel with empty state ("You're all caught up 🏺").
3. Log in as **Admin** → confirm notification FAB appears (no badge), opens panel with empty state.
4. Log out → confirm FAB is hidden.
5. Navigate to Admin dropdown → confirm **no Features link** is present.
6. Navigate to Tourist/Guest nav → confirm **Features link is still present**.
7. Resize to mobile width (< 576px) → confirm FABs do not overlap or collide; gap remains ~12px.
8. Grep `fa-`, `fas-`, `fab-` across `Views/` → confirm zero matches.
