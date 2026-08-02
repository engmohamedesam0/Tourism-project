# EGYXPLORE — Dark/Light Mode Color Audit & Fix Plan

## Phase 1: AUDIT — Scan Results

### A. Hardcoded Colors Found

#### CSS Files

| File | Line(s) | Hardcoded Value | Issue |
|------|---------|-----------------|-------|
| `site.css` | 690 | `#1a0f00` in `.stats-section` gradient | Not theme-aware; should use `--egy-dark` |
| `site.css` | 765 | `rgba(26, 18, 11, 0.04)` in `.stats-section .stat-card` | Should use `--bg-surface` or `--bg-card` |
| `site.css` | 831 | `rgba(232, 221, 208, 0.55)` in `.stats-section .stat-label` | Hardcoded light-mode-only text on dark bg |
| `site.css` | 969 | `rgba(255,255,255,0.8)` in `.utility-link` | Light-mode-only; no dark override |
| `site.css` | 999 | `rgba(255,255,255,0.8)` in `#mainNav .nav-link` | Light-mode-only; no dark override |
| `site.css` | 1034 | `rgba(255,255,255,0.3)` in `.nav-spacer` | Light-mode-only |
| `site.css` | 1203 | `rgba(255,255,255,0.75)` in `.main-footer a` | Light-mode-only text on dark bg |
| `site.css` | 1431 | `rgba(255,255,255,0.85)` in `.badge-toast .toast-body` | Light-mode-only |
| `site.css` | 1647 | `rgba(255,255,255,0.5)` in `.text-black-50` dark override | Should use `--text-muted` |
| `site.css` | 1671 | `rgba(var(--bs-primary-rgb), var(--bs-text-opacity))` | Uses `var(--bs-text-opacity)` which may not be set |
| `sponsor-dashboard.css` | 189 | `#888` in `.kpi-sub` | Hardcoded gray, no dark override |
| `sponsor-dashboard.css` | 211 | `#ffffff` in `.dash-header-card` | No dark-mode override for bg |
| `sponsor-dashboard.css` | 211 | `#ffffff` in `.kpi-card` | No dark-mode override for bg |
| `sponsor-dashboard.css` | 211 | `#ffffff` in `.dash-card` | No dark-mode override for bg |
| `sponsor-dashboard.css` | 211 | `#ffffff` in `.dash-map-card` | No dark-mode override for bg |
| `sponsor-dashboard.css` | 211 | `#ffffff` in `.chart-card` | No dark-mode override for bg |
| `sponsor-dashboard.css` | 362 | `#ece6db` in `.dash-map-container` | Hardcoded map bg, no dark override |
| `sponsor-dashboard.css` | 421 | `#fdfdfd` in `.chip-btn` | No dark-mode override |
| `sponsor-dashboard.css` | 503 | `#ffffff` in `.top-performing-card` gradient | No dark override |
| `sponsor-dashboard.css` | 512 | `#ffffff` in `.top-branch-item` | No dark override |
| `admin-dashboard.css` | 9 | `#F8FAFC` in `.dashboard-wrapper` | No dark override for bg |
| `admin-dashboard.css` | 10 | `#FFFFFF` in `.dash-card-bg` var | Dark override exists but some cards miss it |
| `admin-dashboard.css` | 56 | `#EDF2F7` in `.admin-segmented-switch` | No dark override |
| `admin-dashboard.css` | 364 | `#FAFAFA` in `.map-card-bar` | No dark override |
| `login.css` | 43 | `#f8f5ee` in `.auth-page-wrapper` bg | No dark override for bg |
| `login.css` | 88 | `#fbfaf7` / `#f5f0e7` in `.form-panel` gradient | No dark override |
| `login.css` | 214 | `rgba(255,255,255,.95)` in `.input-wrap` bg | No dark override |
| `login.css` | 487 | `#fff` in `.social-icon-btn` bg | No dark override |
| `login.css` | 530 | `#0c1115` in `.hero-panel` bg | No dark override |
| `login.css` | 555 | `#f7f7f7` in `.hero-title` | No dark override |
| `docs.css` | 10 | `--docs-text: #1d1d1f` | Dark override exists at line 743 |
| `docs.css` | 492 | `#1d1d1f` in `.docs-article pre` bg | Dark override exists at line 763 |
| `docs.css` | 560 | `#ffffff` in `.docs-callout` bg | No explicit dark override for callout bg |
| `docs.css` | 951 | `#ffffff` in `.docs-filter-menu` bg | Dark override exists |
| `theme-toggle.css` | 49-53 | `#C8832A` in SVG drop-shadow | Light-mode-only; dark override exists at line 67 |
| `favorites.css` | 9 | `rgba(255,255,255,0.85)` in `.favorite-btn` bg | No dark override for light-mode bg |
| `favorites.css` | 39 | `var(--egy-text-muted, #6b7280)` in `.favorite-btn-icon` | Fallback is light-mode only |

#### CSHTML Files (Inline Styles / Hardcoded Colors)

| File | Line(s) | Hardcoded Value | Issue |
|------|---------|-----------------|-------|
| `_Layout.cshtml` | 120 | `rgba(255,255,255,0.2)` in `.nav-spacer` | Light-mode only |
| `_Layout.cshtml` | 122 | `rgba(255,255,255,0.3)` in `.nav-spacer` | Light-mode only |
| `_Layout.cshtml` | 559 | `rgba(255,255,255,0.8)` in `.utility-link` | Light-mode only |
| `_Layout.cshtml` | 978 | `#f5c77e 0%, #C8832A 50%, #E8A045 100%` in welcome overlay gradient | Hardcoded; no dark override |
| `_Layout.cshtml` | 953 | `rgba(10, 5, 0, 0.7)` in welcome overlay bg | No dark override |
| `Home/Index.cshtml` | 407 | `#4ade80` in `.badge-dot` | Hardcoded green; no dark override |
| `Home/Index.cshtml` | 422 | `#ffffff` in `.hero-title` | No dark override |
| `Home/Index.cshtml` | 430 | `#E8A045 0%, #C8832A 50%, #f5c77e 100%` gradient | Hardcoded; no dark override |
| `Home/Index.cshtml` | 458 | `rgba(255,255,255,0.72)` in `.hero-subtitle` | Light-mode only |
| `Home/Index.cshtml` | 517 | `rgba(200,131,42,0.6)` in `.btn-hero-ghost` border | Light-mode only |
| `Home/Index.cshtml` | 517 | `rgba(255,255,255,0.85)` in `.btn-hero-ghost` color | Light-mode only |
| `Home/Index.cshtml` | 694-697 | `#7fb2ff`, `#6fe3a0`, `#c9a3fb` icon colors | Hardcoded; no dark override |
| `Home/Index.cshtml` | 703 | `#ffffff` in `.stat-pill-num` | No dark override |
| `Home/Index.cshtml` | 715 | `rgba(255,255,255,0.5)` in `.stat-pill-lbl` | Light-mode only |
| `Home/Index.cshtml` | 773 | `#555` in `.brand-body` | No dark override |
| `Home/Index.cshtml` | 861 | `#777` in `.section-sub` | No dark override |
| `Home/Index.cshtml` | 965 | `rgba(255,255,255,0.65)` in `.bento-text` | Light-mode only |
| `Home/Index.cshtml` | 1148 | `#fff` in `.ts-content h4` | No dark override |
| `Home/Index.cshtml` | 1151 | `rgba(255,255,255,0.5)` in `.ts-content p` | Light-mode only |
| `Home/Index.cshtml` | 1227 | `#666` in `.pillar-card p` | No dark override |
| `Home/Index.cshtml` | 1239 | `#888` in `.pillar-tag` | No dark override |
| `Home/Index.cshtml` | 1287 | `rgba(255,255,255,0.65)` in `.cta-sub` | Light-mode only |
| `Explore/Index.cshtml` | 369 | `#ece6db` in `.explore-map-panel` bg | No dark override |
| `Explore/Index.cshtml` | 375 | `#e7e0d5` / `#efe9df` in `.explore-map-container` gradient | No dark override |
| `Explore/Index.cshtml` | 309 | `#4a4a4a` in `.text-secondary-dark` | No dark override |
| `Explore/Index.cshtml` | 306 | `#777` in `.rating-new` | No dark override |
| `Explore/Index.cshtml` | 107 | `var(--egy-primary)` in empty state icon | OK — uses variable |
| `Destination/Index.cshtml` | 462 | `#6c757d` in `.destination-page-subtitle` | No dark override |
| `Destination/Index.cshtml` | 700 | `#6c757d` in `.destination-desc` | No dark override |
| `Destination/Index.cshtml` | 719 | `#495057` in `.destination-city` | No dark override |
| `Destination/Index.cshtml` | 736 | `#6c757d` in `.destination-visits .bi` | No dark override |
| `Destination/Index.cshtml` | 747 | `#b8860b` in `.destination-rating` | No dark override |
| `Destination/Index.cshtml` | 746 | `#fff8e6` in `.destination-rating` bg | No dark override |
| `Destination/Index.cshtml` | 807 | `#d4edda` in `.badge-status-active` | No dark override |
| `Destination/Index.cshtml` | 812 | `#856404` in `.badge-status-pending` | No dark override |
| `Destination/Index.cshtml` | 817 | `#721c24` in `.badge-status-inactive` | No dark override |
| `Destination/Index.cshtml` | 568 | `#FDF6EC` in `.destination-active-chip` bg | No dark override |
| `Destination/Index.cshtml` | 569 | `#E8A045` in `.destination-active-chip` border | No dark override |
| `Destination/Index.cshtml` | 675 | `#f5e6cc` in `.destination-avatar` gradient | No dark override |
| `Destination/Index.cshtml` | 839 | `#f5e6cc` in `.destination-empty-icon` gradient | No dark override |
| `AdminDashboard/Index.cshtml` | 137 | `#1E120A` in `Chart.defaults.color` | Hardcoded; should use theme-aware value |
| `AdminDashboard/Index.cshtml` | 139 | `#C8832A` in `themePrimary` | OK — brand color |
| `AdminDashboard/Index.cshtml` | 140 | `#1E120A` in `themeDark` | Hardcoded; should be theme-aware |
| `_Layout.cshtml` | 559 | `rgba(255,255,255,0.8)` in `notifEmptyHtml` | Light-mode only |

### B. Components Ignoring Active Theme

| Component | Issue | Root Cause |
|-----------|-------|------------|
| `.notification-widget-panel` | `background: #fff` in aiChat.css:626 | No `[data-theme="dark"]` override for bg |
| `.notification-widget-body` | `background: var(--egy-light)` — `#F8FAFC` in light, but no dark override | Missing dark bg variable |
| `.ai-widget-panel` | `background: #fff` in aiChat.css:70 | Has dark override at line 771, but `#fff` fallback is light-only |
| `.ai-menu-dropdown` | `background: #fff` in aiChat.css:184 | Has dark override at line 783 |
| `.dropdown-menu:not(.dropdown-menu-dark)` | Uses `--bs-dropdown-bg` but Bootstrap default is `#fff` | Properly overridden in site.css:232-244 |
| `.toast` | Uses Bootstrap default light styling | Has dark override in site.css:261-265 |
| `.modal-content` | Uses Bootstrap default | Has dark override in site.css:214-229 |
| `.popover` | Bootstrap default | No explicit dark-mode override in custom CSS |
| `.badge.text-bg-danger` | Hardcoded Bootstrap danger colors | No dark-mode override for badge colors |
| `.badge.text-bg-success` | Hardcoded Bootstrap success colors | No dark-mode override |
| `.badge.text-bg-warning` | Hardcoded Bootstrap warning colors | No dark-mode override |
| `.badge.text-bg-info` | Hardcoded Bootstrap info colors | No dark-mode override |
| `.badge.bg-light` | Has dark override in site.css:530-533 | OK |
| `.badge.text-dark` | Has dark override in site.css:535-537 | OK |
| `.form-control` | Has dark override in site.css:141-165 | OK |
| `.form-select` | Has dark override in site.css:141-165 | OK |
| `.input-group-text` | Has dark override in site.css:173-177 | OK |
| `.list-group-item` | Has dark override in site.css:462-466 | OK |
| `.accordion-item` | Has dark override in site.css:469-473 | OK |
| `.nav-tabs .nav-link` | Has dark override in site.css:490-504 | OK |
| `.pagination .page-link` | Has dark override in site.css:507-522 | OK |
| `.progress` | Has dark override in site.css:525-527 | OK |
| `.table` | Has dark override in site.css:315-346 | OK |
| `.card` | Has dark override in site.css:195-204 | OK |
| `.btn-outline-secondary` | Has dark override in site.css:349-358 | OK |
| `.btn-light` | Has dark override in site.css:360-371 | OK |
| `.btn-outline-light` | Has dark override in site.css:373-382 | OK |
| `.btn-warning` | Has dark override in site.css:384-386 | OK |
| `.btn-close` | Has dark override in site.css:388-390 | OK |
| `.text-dark`, `.text-body`, `.text-black` | Has dark override in site.css:393-397 | OK |
| `.text-muted` | Has dark override in site.css:404-406 | OK |
| `.text-secondary` | Has dark override in site.css:408-410 | OK |
| `.bg-white`, `.bg-light` | Has dark override in site.css:417-420 | OK |
| `.bg-body` | Has dark override in site.css:422-424 | OK |
| `.bg-secondary` | Has dark override in site.css:426-428 | OK |
| `.border`, `.border-top`, etc. | Has dark override in site.css:431-437 | OK |
| `.border-light` | Has dark override in site.css:439-441 | OK |
| `.border-white` | Has dark override in site.css:443-445 | OK |

### C. Contrast/Readability Issues

| Element | Light Mode | Dark Mode | Issue |
|---------|-----------|-----------|-------|
| `.text-secondary` (`#6c757d`) on `--bg-surface` (`#fff`) | 4.5:1 ✓ | ~2.5:1 ✗ | Invisible in dark mode without override |
| `.text-muted` (`#6b7280`) on `--bg-card` (`#1A120B`) | ~4.6:1 ✓ | ~3.2:1 ✗ | Below AA in dark mode |
| `.badge-status-pending` (`#856404` on `rgba(228,198,98,0.15)`) | ~3.5:1 ✗ | Invisible ✗ | Low contrast in both modes |
| `.badge-status-inactive` (`#721c24` on `#f8d7da`) | ~5.2:1 ✓ | Invisible ✗ | No dark override |
| `.badge-status-active` (`#155724` on `#d4edda`) | ~5.8:1 ✓ | Invisible ✗ | No dark override |
| `.destination-rating` (`#b8860b` on `#fff8e6`) | ~3.8:1 ✗ | Invisible ✗ | Low contrast, no dark override |
| `.rating-new` (`#777` on transparent) | ~4.2:1 ✓ | ~2.8:1 ✗ | No dark override |
| `.text-secondary-dark` (`#4a4a4a`) | ~5.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.kpi-sub` (`#888` in sponsor-dashboard.css) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.destination-page-subtitle` (`#6c757d`) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.destination-desc` (`#6c757d`) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.destination-visits .bi` (`#6c757d`) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.destination-rating` (`#b8860b`) | ~3.8:1 ✗ | Invisible ✗ | Below AA in both modes |
| `.destination-active-chip` (`#C8832A` on `#FDF6EC`) | ~4.5:1 ✓ | Invisible ✗ | No dark override |
| `.destination-avatar` gradient (`#FDF6EC` to `#f5e6cc`) | Light only | Invisible ✗ | No dark override |
| `.destination-empty-icon` gradient | Light only | Invisible ✗ | No dark override |
| `.map-watermark` (`var(--egy-muted-gold)` opacity 0.7) | ~3.5:1 ✗ | ~2.5:1 ✗ | Low contrast |
| `.explore-map-container` (`#ece6db` bg) | Light only | No dark override |
| `.features-bento` (`#f4f0eb` bg) | Light only | No dark override |
| `.bento-dark` (`#1E120A` to `#2c1a0e`) | Dark only | OK |
| `.bento-gradient` (`var(--egy-dark)` to `#2a1800`) | Dark only | OK |
| `.howit-section` gradient | Dark only | OK |
| `.cta-banner` bg image | No theme adaptation | N/A |
| `.brand-section` (`var(--egy-light)` = `#F8FAFC`) | Light only | No dark override |
| `.pillar-card` (`var(--bg-surface)`) | OK | OK (via variable) |
| `.docs-hero` gradient | Light only | No dark override |
| `.docs-callout` (`var(--docs-bg-subtle)`) | OK | Has dark override |
| `.docs-pagination a` | OK | No explicit dark override for box-shadow |
| `.docs-sidebar` border | OK | Has dark override |
| `.docs-search-wrap .form-control` | OK | No explicit dark override |
| `.docs-topbar` border | OK | No explicit dark override |
| `.docs-article table` border | OK | No explicit dark override |
| `.docs-article th` bg (`var(--docs-bg-subtle)`) | OK | Has dark override |
| `.docs-article code` bg (`var(--docs-bg-subtle)`) | OK | Has dark override |
| `.docs-copy-btn` | OK | Has dark override |
| `.docs-filter-item` | OK | Has dark override |
| `.doc-card` box-shadow | OK | Has dark override |
| `.docs-download-pdf` | OK | No explicit dark override |
| `.notification-badge` (`#dc3545` on `#fff`) | OK | No dark override |
| `.notification-fab-badge` (`var(--egy-danger)` on `#fff`) | OK | No dark override |
| `.ai-chat-msg-error .ai-chat-bubble` (`#fdecea` bg) | Light only | Has dark override |
| `.ai-chat-msg-error .ai-chat-bubble` (`#b3261e` text) | Light only | Has dark override |
| `.ai-history-item` border (`rgba(0,0,0,0.04)`) | Light only | No dark override |
| `.ai-history-item-date` (`#888`) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |
| `.ai-history-empty` (`#888`) | ~4.5:1 ✓ | ~2.5:1 ✗ | No dark override |

## Phase 2: DIAGNOSE — Root Causes

1. **Missing dark-mode CSS overrides**: Many components (badges, chips, map panels, filter panels, notification panels) lack `[data-theme="dark"]` overrides entirely.
2. **Hardcoded hex/rgb values**: Colors like `#ffffff`, `#000000`, `#6c757d`, `#888`, `#4a4a4a`, `#777`, `#ece6db`, `#f4f0eb`, `#FDF6EC`, `#fff8e6` are hardcoded without CSS variable references.
3. **Bootstrap defaults without themed overrides**: Components like `.popover`, `.tooltip`, `.badge.bg-light`, `.badge.text-bg-*` rely on Bootstrap's default light-mode styling.
4. **Inline styles in CSHTML**: Several pages use inline `style` attributes with hardcoded colors that bypass CSS variable theming.
5. **JS-rendered components not receiving theme**: Chart.js colors in `AdminDashboard/Index.cshtml` are hardcoded (`#1E120A`, `#C8832A`) and don't respond to theme changes.
6. **Wrong CSS specificity/cascade**: Some dark-mode overrides exist but are overridden by more specific selectors or inline styles.
7. **Missing `--egy-light` dark override**: The `--egy-light` variable (`#F8FAFC`) is used as a background in several places but has no dark-mode equivalent defined.

## Phase 3: FIX — Implementation Plan

### Task 1: Centralize Theme Variables in `site.css`

Add missing CSS variables to `:root` and `[data-theme="dark"]` in `site.css`:

- Add `--bg-map: #ece6db` (light) and `--bg-map-dark: #1a120b` (dark)
- Add `--bg-features: #f4f0eb` (light) and `--bg-features-dark: #1a120b` (dark)
- Add `--bg-chip: #fdfdfd` (light) and `--bg-chip-dark: #231911` (dark)
- Add `--text-secondary-dark: #4a4a4a` (light) and `--text-secondary-dark-dark: #a89b8c` (dark)
- Add `--text-muted-light: #777` (light) and `--text-muted-dark: #8b7d6b` (dark)
- Add `--badge-active-bg: #d4edda` / `--badge-active-text: #155724` with dark overrides
- Add `--badge-pending-bg: rgba(228,198,98,0.15)` / `--badge-pending-text: #856404` with dark overrides
- Add `--badge-inactive-bg: #f8d7da` / `--badge-inactive-text: #721c24` with dark overrides
- Add `--rating-bg: #fff8e6` / `--rating-text: #b8860b` with dark overrides
- Add `--chip-active-bg: #FDF6EC` / `--chip-active-border: #E8A045` with dark overrides
- Add `--destination-avatar-bg: #FDF6EC` / `--destination-avatar-border: rgba(200,131,42,0.25)` with dark overrides

### Task 2: Replace Hardcoded Colors with CSS Variables

In `site.css`, replace all hardcoded color values with CSS variables:

- `#ffffff` → `var(--bg-surface)` or `var(--bg-card)`
- `#000000` / `#1E120A` text → `var(--text-heading)` or `var(--text-primary)`
- `#6c757d` → `var(--text-secondary)` or `var(--text-muted)`
- `#888` / `#999` / `#777` → `var(--text-muted)`
- `#4a4a4a` → `var(--text-secondary-dark, var(--text-muted))`
- `#ece6db` → `var(--bg-map)`
- `#f4f0eb` → `var(--bg-features)`
- `#FDF6EC` → `var(--chip-active-bg)`
- `#fff8e6` → `var(--rating-bg)`
- `#b8860b` → `var(--rating-text)`
- `#d4edda` → `var(--badge-active-bg)`
- `#155724` → `var(--badge-active-text)`
- `#f8d7da` → `var(--badge-inactive-bg)`
- `#721c24` → `var(--badge-inactive-text)`
- `rgba(228,198,98,0.15)` → `var(--badge-pending-bg)`
- `#856404` → `var(--badge-pending-text)`

### Task 3: Add Dark-Mode Overrides for Missing Components

Add `[data-theme="dark"]` overrides in `site.css` for:

- `.badge-status-active`, `.badge-status-pending`, `.badge-status-inactive`
- `.destination-active-chip`, `.destination-avatar`, `.destination-empty-icon`
- `.destination-rating`, `.destination-visits`, `.destination-city`, `.destination-desc`
- `.destination-page-subtitle`, `.destination-name`, `.destination-price`
- `.explore-map-panel`, `.explore-map-container`
- `.features-bento`
- `.brand-section`
- `.section-sub`, `.pillar-card p`, `.pillar-tag`
- `.ts-content h4`, `.ts-content p`
- `.stat-pill-num`, `.stat-pill-lbl`
- `.hero-title`, `.hero-subtitle`, `.btn-hero-ghost`
- `.bento-text`, `.bento-dark .bento-text`
- `.badge-dot`
- `.notification-widget-panel`, `.notification-widget-body`
- `.ai-history-item-date`, `.ai-history-empty`
- `.docs-hero`, `.docs-callout`, `.docs-topbar`, `.docs-article table`
- `.docs-search-wrap .form-control`, `.docs-sidebar`
- `.docs-pagination a`
- `.docs-download-pdf`
- `.popover`, `.tooltip`
- `.kpi-sub` (sponsor dashboard)
- `.chip-btn` (sponsor dashboard)
- `.top-branch-item`, `.top-performing-card`
- `.map-card-bar`
- `.auth-page-wrapper`, `.form-panel`, `.input-wrap`, `.social-icon-btn`
- `.hero-panel`, `.hero-title`, `.hero-description`
- `.cta-banner`
- `.howit-section`
- `.bento-dark`, `.bento-gradient`

### Task 4: Fix CSHTML Inline Styles

- `_Layout.cshtml`: Replace inline `rgba(255,255,255,0.2)` and `rgba(255,255,255,0.3)` with CSS variables
- `_Layout.cshtml`: Replace welcome overlay `rgba(10,5,0,0.7)` with theme-aware variable
- `Home/Index.cshtml`: Replace `#4ade80` badge-dot with CSS variable
- `Home/Index.cshtml`: Replace `#ffffff` hero-title with CSS variable
- `Home/Index.cshtml`: Replace `rgba(255,255,255,0.72)` hero-subtitle with CSS variable
- `Home/Index.cshtml`: Replace `#777` section-sub with CSS variable
- `Home/Index.cshtml`: Replace `#555` brand-body with CSS variable
- `Home/Index.cshtml`: Replace `#666` pillar-card p with CSS variable
- `Home/Index.cshtml`: Replace `#888` pillar-tag with CSS variable
- `Home/Index.cshtml`: Replace `rgba(255,255,255,0.65)` bento-text/ts-content p with CSS variable
- `Home/Index.cshtml`: Replace `#fff` ts-content h4 with CSS variable
- `Home/Index.cshtml`: Replace `#fff` hero-title with CSS variable
- `Explore/Index.cshtml`: Replace `#ece6db` map panel bg with CSS variable
- `Explore/Index.cshtml`: Replace `#4a4a4a` text-secondary-dark with CSS variable
- `Explore/Index.cshtml`: Replace `#777` rating-new with CSS variable
- `Destination/Index.cshtml`: Replace `#6c757d` text colors with CSS variables
- `Destination/Index.cshtml`: Replace `#495057` destination-city with CSS variable
- `Destination/Index.cshtml`: Replace `#b8860b` rating text with CSS variable
- `Destination/Index.cshtml`: Replace `#fff8e6` rating bg with CSS variable
- `Destination/Index.cshtml`: Replace `#FDF6EC` chip bg with CSS variable
- `Destination/Index.cshtml`: Replace `#E8A045` chip border with CSS variable
- `Destination/Index.cshtml`: Replace `#f5e6cc` avatar gradient with CSS variable
- `Destination/Index.cshtml`: Replace hardcoded badge colors with CSS variables
- `AdminDashboard/Index.cshtml`: Replace hardcoded Chart.js colors with theme-aware JS

### Task 5: Fix JS-Rendered Components

- `AdminDashboard/Index.cshtml`: Make Chart.js colors theme-aware by reading `data-theme` attribute or dispatching theme change events
- `_Layout.cshtml`: Ensure notification panel JS renders theme-aware content
- `aiChat.js` (if exists): Ensure chat bubble colors adapt to theme

### Task 6: Fix Notification Panel and AI Widget

- `aiChat.css`: Ensure `.notification-widget-panel` has proper dark-mode background override
- `aiChat.css`: Ensure `.notification-widget-body` uses theme-aware background
- `aiChat.css`: Ensure `.ai-history-item-date` and `.ai-history-empty` have dark-mode colors

### Task 7: Fix Badge/Chip/Pill Contrast

- Ensure all `.badge-*` classes have dark-mode overrides
- Ensure all `.chip-btn` classes have dark-mode overrides
- Ensure all `.tag-pill` classes have dark-mode overrides
- Ensure all `.rating-badge` classes have dark-mode overrides
- Ensure all `.destination-status-btn` classes have dark-mode overrides

## Phase 4: VERIFY

### Pages to Verify (Light + Dark Mode)

1. Home (`/Home/Index`) — hero, stats band, features, how-it-works, pillars, CTA
2. Explore (`/Explore/Index`) — map panel, filter chips, destination cards, empty state
3. Destinations (`/Destination/Index`) — table, badges, filter chips, empty state
4. Admin Dashboard (`/AdminDashboard/Index`) — KPI cards, charts, sidebar, map
5. Sponsor Dashboard (`/SponsorPortal/Dashboard`) — KPI cards, map, charts, chips
6. Trip Planner (`/Trip/Index`) — timeline, cards
7. Rewards (`/Reward/Index`) — cards, badges
8. Tourist Profile (`/TouristProfile/Index`) — profile card, stats
9. Login (`/Account/Login`) — form panel, hero panel
10. Register (`/Account/Register`) — form panel
11. Docs (`/Docs/Landing`, `/Docs/Article`) — sidebar, search, callouts, tables
12. Near Me (`/NearMe/Index`) — map, list
13. Sponsor Portal (`/SponsorPortal/Index`) — dashboard layout
14. Sponsor Notifications (`/SponsorNotification/Index`) — panel, badges
15. Admin Support (`/AdminSupport/Index`) — table, cards
16. Tourist Support (`/TouristSupport/Index`) — table, cards
17. Favorites (`/Favorite/Index`) — cards, favorite buttons
18. About (`/About/Index`) — brand section
19. Features (`/Features/Index`) — feature cards
20. Privacy (`/Home/Privacy`) — simple page
21. Error (`/Shared/Error`) — error page
22. Access Denied (`/Account/AccessDenied`) — auth page

### Components to Verify

- [ ] Navbar (utility + primary) — text, bg, links in both modes
- [ ] Dropdown menus (lang switcher, account, admin, sponsor, support) — bg, text, borders
- [ ] Modals — bg, text, borders, close button
- [ ] Offcanvas — bg, text, borders
- [ ] Popovers — bg, text, borders, arrow
- [ ] Toasts — bg, text, borders, close button
- [ ] Notification FAB + panel — bg, text, borders, icons
- [ ] AI Chat widget — panel bg, header, message bubbles, input, footer
- [ ] Theme toggle — SVG icon colors, tooltip
- [ ] ViewComponents (TouristLevelBadge, SupportBell, NotificationFab, AdminNavBadges, AdminApprovalBell) — colors in both modes
- [ ] Cards — bg, border, text
- [ ] Tables — bg, text, borders, striped rows, hover
- [ ] Forms — inputs, labels, placeholders, validation
- [ ] Buttons — all variants (primary, secondary, success, danger, warning, info, light, dark, outline)
- [ ] Badges/Pills — all variants, active/inactive states
- [ ] Filter chips — active/inactive states
- [ ] Pagination — links, active, disabled
- [ ] Progress bars — track, fill
- [ ] Accordion — items, buttons
- [ ] List groups — items, active, hover
- [ ] Alerts — all variants
- [ ] Tabs — active, inactive
- [ ] Breadcrumbs — links, active, dividers
- [ ] Tooltips — bg, text
- [ ] Popovers — bg, text, borders

## Phase 5: FILES CHANGED SUMMARY

| File | Changes |
|------|---------|
| `wwwroot/css/site.css` | Add missing CSS variables, add dark-mode overrides for all missing components, replace hardcoded colors with variables |
| `wwwroot/css/sponsor-dashboard.css` | Add dark-mode overrides for KPI sub text, chip buttons, map container, cards, top branch items |
| `wwwroot/css/admin-dashboard.css` | Add dark-mode overrides for segmented switch bg, map card bar, leaderboard, activity icons |
| `wwwroot/css/login.css` | Add dark-mode overrides for auth page wrapper, form panel, input wraps, social buttons, hero panel |
| `wwwroot/css/aiChat.css` | Fix notification widget panel/body dark overrides, AI history item date/empty dark overrides |
| `wwwroot/css/favorites.css` | Fix favorite button bg dark override |
| `wwwroot/css/docs.css` | Add dark-mode overrides for docs hero, callout, topbar, table borders, pagination |
| `wwwroot/css/rtl.css` | No changes needed (RTL is direction-only) |
| `wwwroot/css/theme-toggle.css` | No changes needed |
| `Views/Shared/_Layout.cshtml` | Replace inline hardcoded rgba colors with CSS variables, fix welcome overlay |
| `Views/Home/Index.cshtml` | Replace inline hardcoded colors with CSS variables |
| `Views/Explore/Index.cshtml` | Replace inline hardcoded colors with CSS variables |
| `Views/Destination/Index.cshtml` | Replace inline hardcoded colors with CSS variables |
| `Views/AdminDashboard/Index.cshtml` | Make Chart.js colors theme-aware |
| `Views/Shared/Components/NotificationFab/Default.cshtml` | No changes (uses CSS classes) |
| `Views/Shared/Components/SupportBell/Default.cshtml` | No changes (uses CSS classes) |
| `Views/Shared/Components/AdminNavBadges/Default.cshtml` | No changes (uses CSS classes) |
| `Views/Shared/Components/AdminApprovalBell/Default.cshtml` | No changes (uses CSS classes) |
| `Views/Shared/Components/TouristLevelBadge/Default.cshtml` | No changes (uses CSS classes) |
| `Views/Shared/_ThemeToggle.cshtml` | No changes |
| `Views/Shared/_AdminModuleToggle.cshtml` | Verify theme awareness |
| `Views/Shared/_StatBoxRow.cshtml` | Verify theme awareness |
| `Views/Shared/_ReviewsCarousel.cshtml` | Verify theme awareness |
| `Views/Shared/_FavoriteButton.cshtml` | Verify theme awareness |
| `Views/Shared/_EgyxploreWordmark.cshtml` | Verify theme awareness |
| `Views/Shared/_SuccessLottie.cshtml` | Verify theme awareness |
| `Views/Shared/Error.cshtml` | Verify theme awareness |
| `Views/Shared/_ValidationScriptsPartial.cshtml` | No changes |
| `wwwroot/js/theme-engine.js` | No changes needed (already dispatches themechange event) |
| `wwwroot/js/scripts.js` | Verify theme-aware behavior |
| `wwwroot/js/maps.js` | Verify theme-aware map colors |

## Open Questions

1. Does `scripts.js` contain any hardcoded colors that need theme-aware updates?
2. Does `maps.js` contain hardcoded marker colors that need theme-aware updates?
3. Are there any other JS files that render colors dynamically?
4. Does the `_AdminModuleToggle.cshtml` partial have theme issues?
5. Does the `_StatBoxRow.cshtml` partial have theme issues?
6. Does the `_ReviewsCarousel.cshtml` partial have theme issues?
7. Does the `_FavoriteButton.cshtml` partial have theme issues?
8. Does the `_EgyxploreWordmark.cshtml` partial have theme issues?
9. Does the `_SuccessLottie.cshtml` partial have theme issues?
10. Are there any other `.cshtml` files with inline `style` attributes containing hardcoded colors?