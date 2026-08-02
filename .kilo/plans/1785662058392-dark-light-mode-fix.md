# EGYXPLORE Dark/Light Mode Fix Plan

## 1. AUDIT

### Theme System Architecture
- **Custom `data-theme` system**: Project uses `data-theme="dark"` / `data-theme="light"` on `<html>`, **NOT** Bootstrap 5.3's `data-bs-theme`
- **Bootstrap version**: 5.2.3 (custom build in `styles.css`) — does NOT support `data-bs-theme`
- **Toggle mechanism**: `theme-engine.js` stores `"dark" | "light" | "system"` in `localStorage['egyxplore-theme']`, respects `prefers-color-scheme`, dispatches `themechange` event
- **FOUC prevention**: Inline script in `_Layout.cshtml` `<head>` (lines 4-10) sets `data-theme` before CSS loads

### CSS Files With Dark Mode (`[data-theme="dark"]`)
| File | Has `[data-theme="dark"]` | Notes |
|------|---------------------------|-------|
| `site.css` | Yes (lines 87-123, 128+) | Core theme tokens + Bootstrap component overrides |
| `admin-dashboard.css` | Yes (line 491+) | Dashboard-specific tokens |
| `sponsor-dashboard.css` | Yes (line 630+) | Dashboard-specific tokens |
| `docs.css` | Yes (line 742+) | Docs page overrides |
| `aiChat.css` | Yes (line 771+) | AI widget + notification panel |
| `theme-toggle.css` | Yes (line 66+) | Toggle button SVG animation |
| `login.css` | **NO** | Login page has zero dark mode support |
| `favorites.css` | **NO** | Favorite button has no dark mode support |
| `rtl.css` | **NO** | RTL layout only, no theme awareness |

### Variable Definitions
- **`--bs-primary`**: `#C8832A` in `styles.css:37` — **already matches** `--egy-primary`
- **`--egy-primary`**: `#C8832A` in `site.css:4`
- **`--egy-dark`**: `#1E120A` in `site.css:6`
- **`--bs-primary-rgb`**: `200, 131, 42` in `styles.css:47`

### Bootstrap Component Color Bugs (Hardcoded Teal)
Despite `--bs-primary` being Egyptian gold, these Bootstrap components retain **hardcoded teal** values from the original Bootstrap 5.2.3 build:

**`styles.css:2853-2868` — `.btn-primary`**:
- `--bs-btn-hover-bg: #558985` (teal — should be `#b37424`)
- `--bs-btn-focus-shadow-rgb: 123, 175, 172` (teal — should be `200, 131, 42`)
- `--bs-btn-active-border-color: #4b7976` (teal — should be `#b37424`)

**`styles.css:4733-4740` — `.alert-primary`**:
- `--bs-alert-color: #3c615e` (teal)
- `--bs-alert-bg: #e0eceb` (teal tint)
- `--bs-alert-border-color: #d1e3e2` (teal)
- `.alert-link: #304e4b` (teal)

### Inline Hardcoded Colors in CSHTML (Non-Theme-Aware)
| File | Line | Issue |
|------|------|-------|
| `Explore/Index.cshtml` | 447 | `style="color:#666"` — poor contrast in dark mode |
| `AdminDashboard/Index.cshtml` | 117-118 | Inline `#1E120A` / `#C8832A` in JS-generated HTML |
| `SponsorPortal/Dashboard.cshtml` | 298-299 | Same inline colors in JS-generated HTML |
| `AdminDashboard/Sections/_Tourists.cshtml` | 146, 167 | `style="color:#6c757d"` — low contrast in dark mode |
| `TouristProfile/Index.cshtml` | 183 | `style="color: #a16468"` — hardcoded danger |
| Multiple views | various | Hero gradients with `linear-gradient(135deg, #1E120A, #C8832A)` |

## 2. DIAGNOSIS

### Root Cause: Incomplete Bootstrap Primary Color Override
The `--bs-primary` variable **was** changed to Egyptian gold (`#C8832A`), but Bootstrap 5.2.3's component classes (`.btn-primary`, `.alert-primary`) use **component-scoped CSS custom properties** with **hardcoded fallback values** that were never updated. This means:

1. **`.btn-primary`** uses teal (`#558985`) for hover background and teal RGB for focus shadow
2. **`.alert-primary`** uses teal shades for all its colors
3. These hardcoded values appear in **both light and dark mode** because they're defined at the base selector level with no dark mode overrides in `site.css`

### Why the Theme Switch Feels Broken
- Users toggle to dark mode and see teal hover states on primary buttons (not Egyptian gold)
- `.alert-primary` shows teal alert boxes instead of gold
- Login page (`login.css`) has **no dark mode support at all** — it stays light-themed
- `favorites.css` has **no dark mode support** — favorite button stays white
- Inline `color:#666` and `color:#6c757d` in views become invisible on dark backgrounds

### Is `--bs-primary` vs `--egy-primary` a Separate Issue?
**No** — they have the **same value** (`#C8832A`). The perceived "conflict" is actually that Bootstrap components don't derive their hover/focus colors from `--bs-primary`. This is **directly part of** the dark/light mode bug because those stale teal values render incorrectly in both modes.

## 3. FIX

### Task A — Fix Bootstrap `.btn-primary` Teal Values (`styles.css`)
Replace hardcoded teal with Egyptian gold equivalents:
- `--bs-btn-hover-bg: #558985` → `#b37424` (`--egy-muted-gold`)
- `--bs-btn-focus-shadow-rgb: 123, 175, 172` → `200, 131, 42`
- `--bs-btn-active-border-color: #4b7976` → `#b37424`

### Task B — Fix Bootstrap `.alert-primary` Teal Values (`styles.css`)
Replace teal alert colors with Egyptian gold equivalents:
- `--bs-alert-color: #3c615e` → `#8B6914` (dark gold text)
- `--bs-alert-bg: #e0eceb` → `#fef3c7` (light gold tint)
- `--bs-alert-border-color: #d1e3e2` → `#fde68a` (gold border)
- `.alert-link: #304e4b` → `#8B6914`

### Task C — Add Dark Mode to `login.css`
Add `[data-theme="dark"]` overrides for:
- `.auth-page-wrapper` background/text
- `.form-panel` background
- `.input-wrap` background/border
- `.hero-panel` overlay adjustments
- Form labels, placeholders, dividers
- Ensure contrast ratios meet WCAG AA on dark backgrounds

### Task D — Add Dark Mode to `favorites.css`
Add `[data-theme="dark"]` overrides for:
- `.favorite-btn` background (use `--bg-surface-elevated`)
- `.favorite-btn-icon` color (use `--text-primary` or `--egy-danger`)

### Task E — Fix Inline Hardcoded Colors in Views
Replace inline hardcoded colors with theme-aware CSS classes or variables:
- `Explore/Index.cshtml:447`: Replace `color:#666` with CSS class `.text-muted-custom`
- `AdminDashboard/Sections/_Tourists.cshtml:146,167`: Replace `color:#6c757d` with `.text-muted`
- `AdminDashboard/Index.cshtml` and `SponsorPortal/Dashboard.cshtml`: Move inline JS-generated color styles to CSS classes

### Task F — Verify Bootstrap Dark Mode Consistency
Add `[data-theme="dark"]` overrides in `site.css` for:
- `.btn-primary` hover/focus states in dark mode
- `.alert-primary` in dark mode (already partially exists but verify)
- `.text-primary` / `.bg-primary` readability on dark backgrounds

## 4. VERIFICATION

### Pages Touched
1. `wwwroot/css/styles.css` — Bootstrap `.btn-primary` and `.alert-primary` color fixes
2. `wwwroot/css/login.css` — Dark mode support added
3. `wwwroot/css/favorites.css` — Dark mode support added
4. `Views/Explore/Index.cshtml` — Inline color fix
5. `Views/AdminDashboard/Sections/_Tourists.cshtml` — Inline color fix
6. `Views/AdminDashboard/Index.cshtml` — Inline color fix (JS HTML generation)
7. `Views/SponsorPortal/Dashboard.cshtml` — Inline color fix (JS HTML generation)

### Regression Spot-Check
- **Admin Dashboard** (`admin-dashboard.css`): Has own `[data-theme="dark"]` — verify no breakage
- **Sponsor Dashboard** (`sponsor-dashboard.css`): Has own `[data-theme="dark"]` — verify no breakage
- **Tourist Dashboard/Profile**: Uses shared layout + `site.css` — verify after changes
- **Home page**: Uses `site.css` hero/stat cards — verify `.btn-primary` hover colors

### Before/After Summary
| Variable/Component | Before | After |
|-------------------|--------|-------|
| `.btn-primary:hover` bg | `#558985` (teal) | `#b37424` (Egyptian muted gold) |
| `.btn-primary:focus` shadow RGB | `123,175,172` (teal) | `200,131,42` (gold) |
| `.btn-primary:active` border | `#4b7976` (teal) | `#b37424` (gold) |
| `.alert-primary` bg | `#e0eceb` (teal tint) | `#fef3c7` (gold tint) |
| `.alert-primary` text | `#3c615e` (teal) | `#8B6914` (dark gold) |
| Login page dark mode | ❌ Not supported | ✅ Supported |
| Favorites button dark mode | ❌ Not supported | ✅ Supported |
| Inline `color:#666` | Hardcoded gray | Theme-aware variable |
