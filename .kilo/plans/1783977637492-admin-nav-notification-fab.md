# Implementation Plan: Admin Nav Cleanup + Sponsor Notification FAB

## Task A: Remove Features Link from Admin Nav

**Scope:** Only remove the `Features` link from the Admin dropdown in `_Layout.cshtml`.  
**Do NOT touch:** `FeaturesController`, `Views/Features/`, the public nav links for Tourist/Guest, or the footer link.

### Change
- **File:** `Views/Shared/_Layout.cshtml`
- **Remove:** Lines 438–442 (the `<li>` with `asp-controller="Features"` inside the `@if (User.IsInRole("Admin"))` block).

### Verification
- Admin users no longer see Features in the Administration dropdown.
- Tourist and Guest nav links for Features remain unchanged.
- Footer "Features" link remains unchanged.

---

## Task B: Sponsor-Only Notification FAB

**Scope:** Replace the existing navbar `NotificationBell` dropdown (sponsor-only) with a floating action button + slide-out panel that uses the exact same CSS/JS pattern as the AI Assistant widget.  
**Do NOT build:** Tourist/Admin notification backend.

### Current State
- `NotificationBellViewComponent` + `NotificationBell/Default.cshtml` render a Bootstrap dropdown `<li>` in the Sponsor navbar.
- The component's inline JS loads `/SponsorNotification/Panel` via AJAX, handles mark-read/delete via global `document.addEventListener('click', ...)`.
- `SponsorNotificationController.Panel()` returns `_NotificationPanel.cshtml` partial.

### B1 — Create `NotificationFabViewComponent`

**New files:**
- `ViewComponents/NotificationFabViewComponent.cs`
- `Views/Shared/Components/NotificationFab/Default.cshtml`

**Component logic:**
- If user is not authenticated or not in Sponsor role → return empty string.
- Resolve sponsor ID the same way `NotificationBellViewComponent` does (`_sponsorRepo.GetAll().Where(s => s.ApplicationUserId == ...).Select(s => s.Id).FirstOrDefault()`).
- If sponsor ID is 0 → return empty string.
- Otherwise → return `UnreadCount = _notificationService.GetUnreadCount(sponsorId)`.

**Default.cshtml output:**
```html
<button type="button"
        id="notificationFabBtn"
        class="notification-fab"
        aria-label="Notifications"
        aria-expanded="false"
        aria-controls="notificationPanel">
  <i class="bi bi-bell-fill"></i>
  @if (Model.UnreadCount > 0)
  {
      <span class="notification-fab-badge" id="notifFabBadge">@Model.UnreadCount</span>
  }
</button>
```

### B2 — Update `_Layout.cshtml`

**Step 1 — Remove old bell from Sponsor nav:**
- Remove line 467: `@await Component.InvokeAsync("NotificationBell")`

**Step 2 — Add notification FAB + panel markup:**
- After the AI FAB button (line 668) and before the AI panel closing `</div>` (line 692), add:
  - `@await Component.InvokeAsync("NotificationFab")` — renders the FAB button with badge
  - The notification panel HTML (mirrors `.ai-widget-panel` structure):
    ```html
    <div class="notification-widget-panel" id="notificationPanel" aria-hidden="true">
      <div class="notification-widget-header">
        <h5 class="notification-widget-title">
          <i class="bi bi-bell-fill me-2"></i> Notifications
        </h5>
        <div class="d-flex align-items-center gap-2">
          <button type="button" class="notification-mark-all-read" id="markAllReadBtn">
            <i class="bi bi-check2-all me-1"></i> Mark all as read
          </button>
          <button type="button" class="notification-widget-close" aria-label="Close">
            <i class="bi bi-x-lg"></i>
          </button>
        </div>
      </div>
      <div class="notification-widget-body" id="notificationPanelBody">
        <div class="text-center py-4">
          <div class="spinner-border text-warning" role="status"></div>
        </div>
      </div>
    </div>
    ```

**Step 3 — Add notification FAB JS (before `</body>`):**
- New `<script>` block that:
  1. Toggles `.notification-widget-open` class on the panel (same pattern as AI widget).
  2. On open, if panel body still contains the spinner, fetch `/SponsorNotification/Panel` and inject HTML into `#notificationPanelBody`.
  3. "Mark all as read" button → POST to `/SponsorNotification/MarkAllRead`, then reload panel content and update badge.
  4. `updateNotifBadge(count)` function that updates `#notifFabBadge` (remove if count ≤ 0, update text if > 0).
- **Port the global click handlers** from `NotificationBell/Default.cshtml` (mark-read on `.notification-item` click, delete on `.notif-delete` click) into this same script block so they work inside the new panel. The selectors (`.notification-item`, `.notif-delete`, `data-notif-id`, etc.) stay identical to preserve compatibility with `_NotificationPanel.cshtml`.

### B3 — CSS for `.notification-fab` and `.notification-widget-panel`

**Add to the existing `<style>` block in `_Layout.cshtml`** (near the AI widget styles):

```css
/* Notification FAB — stacked above AI FAB */
.notification-fab {
    position: fixed;
    right: 22px;
    bottom: 94px;          /* AI FAB bottom(22) + AI FAB height(58) + 14px gap */
    z-index: 1040;
    width: 58px;
    height: 58px;
    border-radius: 50%;
    border: none;
    background: var(--egy-dark);
    color: var(--egy-light);
    font-size: 1.5rem;
    box-shadow: 0 8px 22px rgba(30, 18, 10, 0.35);
    cursor: pointer;
    transition: all 0.25s ease;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.notification-fab:hover,
.notification-fab:focus {
    background: var(--egy-primary);
    color: #fff;
    transform: translateY(-3px) scale(1.05);
    box-shadow: 0 12px 28px rgba(200, 131, 42, 0.45);
}

/* Unread count badge on the FAB */
.notification-fab-badge {
    position: absolute;
    top: -4px;
    right: -4px;
    background: var(--egy-danger);
    color: #fff;
    font-size: 0.7rem;
    font-weight: 700;
    min-width: 20px;
    height: 20px;
    padding: 0 5px;
    border-radius: 50%;
    line-height: 20px;
    text-align: center;
    border: 2px solid #fff;
    font-family: 'Nunito', sans-serif;
}

/* Notification panel — same slide mechanics as AI widget */
.notification-widget-panel {
    position: fixed;
    right: 22px;
    bottom: 166px;         /* notification FAB bottom(94) + FAB height(58) + 14px gap */
    z-index: 1040;
    width: calc(100% - 44px);
    max-width: 360px;
    background: #fff;
    border: 1px solid var(--egy-primary);
    border-radius: 16px;
    overflow: hidden;
    box-shadow: 0 12px 28px rgba(200, 131, 42, 0.25);
    transform: translateY(12px) scale(0.96);
    opacity: 0;
    pointer-events: none;
    transition: all .25s ease;
}

.notification-widget-open .notification-widget-panel,
.notification-widget-panel.notification-widget-open {
    transform: translateY(0) scale(1);
    opacity: 1;
    pointer-events: auto;
}

.notification-widget-header {
    background: var(--egy-dark);
    color: #fff;
    border-bottom: 2px solid var(--egy-primary);
    padding: 12px 16px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.notification-widget-title {
    font-family: 'Cinzel', serif;
    font-size: 1rem;
    margin: 0;
}

.notification-widget-close {
    background: none;
    border: none;
    color: rgba(255,255,255,0.8);
    font-size: 1.2rem;
    cursor: pointer;
    padding: 4px;
    line-height: 1;
    transition: color .2s ease;
}

.notification-widget-close:hover,
.notification-widget-close:focus {
    color: #fff;
    outline: none;
}

.notification-mark-all-read {
    background: transparent;
    border: 1px solid rgba(255,255,255,0.3);
    color: rgba(255,255,255,0.85);
    font-size: 0.8rem;
    padding: 4px 10px;
    border-radius: 999px;
    cursor: pointer;
    transition: all .2s ease;
    white-space: nowrap;
}

.notification-mark-all-read:hover {
    background: rgba(255,255,255,0.1);
    border-color: rgba(255,255,255,0.5);
    color: #fff;
}

.notification-widget-body {
    background: var(--egy-light);
    min-height: 220px;
    max-height: 55vh;
    overflow-y: auto;
    padding: 0;
}

/* Mobile adjustments */
@media (max-width: 575.98px) {
    .notification-widget-panel {
        right: 10px;
        left: 10px;
        bottom: 146px;
        width: auto;
        max-width: none;
    }

    .notification-fab {
        right: 14px;
        bottom: 80px;
        width: 52px;
        height: 52px;
        font-size: 1.3rem;
    }
}
```

### B4 — Remove dead code
- After confirming the new FAB works, the old `NotificationBell` ViewComponent, its `Default.cshtml`, and its `NotificationBellVM` can be deleted. (Keep them until validation passes, then remove.)

---

## Order of Execution

1. **Task A first** — simplest change, no risk of breaking notification flow.
2. **Task B1** — create new ViewComponent + view.
3. **Task B2** — edit `_Layout.cshtml`: remove old bell, add FAB, panel, and JS.
4. **Task B3** — add CSS.
5. **Validate** both tasks (see below).
6. **Task B4** — delete old bell component files.

---

## Validation

### Task A
- Log in as Admin → Administration dropdown must NOT contain "Features".
- Navigate directly to `/Features` → page must still load.
- Footer "Features" link must still work.

### Task B
- Log in as Sponsor → no bell icon in navbar.
- Bottom-right shows two stacked circular buttons: notification FAB (top, dark) and AI FAB (bottom, gold).
- Click notification FAB → panel slides up with same animation as AI panel.
- Panel loads notifications from `/SponsorNotification/Panel`.
- Unread count badge appears on FAB when `UnreadCount > 0`.
- Clicking a notification marks it read (existing AJAX handler works).
- Clicking delete removes it (existing AJAX handler works).
- "Mark all as read" button works.
- Clicking FAB again or the panel close button closes the panel.
- Non-sponsor users (Admin, Tourist, Guest) see neither FAB.
- Mobile: FABs don't overlap, panel fits viewport.

---

## Risks / Edge Cases

- **Z-index collision:** Both FABs and both panels use `z-index: 1040`. The notification panel appears above the FAB but below the AI panel (which is also 1040). Since they're never open simultaneously in normal use, this is fine. If both are open, the DOM order determines stacking (notification panel rendered first, so AI panel sits on top).
- **Global event delegation:** The mark-read/delete handlers are `document`-level listeners. They will fire for notifications in both the new panel and any other rendered notification list. No change needed.
- **Sponsor with no notifications:** Panel shows empty state from `_NotificationPanel.cshtml` (`bi-bell-slash` + "No new notifications").
- **Badge persistence:** Badge count is rendered server-side on initial page load. After mark-read/delete AJAX calls, `updateNotifBadge` updates the DOM badge. If the user navigates to a new page, the count refreshes from the server on the next render.
