# Fix: Account / Language dropdowns render behind the lower navbar

## Status
Two combined causes (report causes **#2** and **#3**). Cause **#4** (transform on upper bar) is **not** present. Cause **#1** is the stacking manifestation of #3.

## Confirmed Findings (in `wwwroot/css/site.css`)

The two dropdowns (language switcher `lang-switcher` and account `account-switcher`) live inside `.navbar-utility` (`_Layout.cshtml:188`), which is the **upper** bar. The **lower** bar `.navbar-primary` sits directly below it.

1. **`.navbar-utility` has `overflow: hidden`** — `site.css:51`. Combined with `max-height: 48px` (`:50`), this **clips** the Bootstrap `.dropdown-menu` (absolutely positioned, extends below the 48px bar). The clipped lower portion is hidden behind the lower bar → exactly the reported symptom. This is the primary clipping cause (#2).

2. **`.navbar-primary` has `backdrop-filter: blur(10px)`** — `site.css:64` (also `-webkit-` `:65`). `backdrop-filter` creates a new **stacking context**. `.navbar-utility` has no `position`/`z-index`, so its descendant dropdown paints in the normal flow layer, which is **below** `.navbar-primary`'s stacking-context layer. Even once clipping is fixed, the menu would still paint behind the lower bar (#3).

3. **No `transform`** on `.navbar-utility` or `#mainNav` (the scroll hide/show uses `max-height`/`opacity` transitions, not `transform`). `styles.css` template rules don't set `overflow`/`transform` on these bars either. So cause #4 is excluded.

## Fix (layering only — no visual/scroll-behavior change)

All edits in `wwwroot/css/site.css`.

### Edit 1 — `.navbar-utility` (lines 46-53)
Remove `overflow: hidden` (so descendant dropdowns are not clipped) and make the bar establish a stacking context **above** the lower bar:
```css
.navbar-utility {
    background-color: rgba(30, 18, 10, 0.9);
    border-bottom: 1px solid rgba(200, 131, 42, 0.25);
    font-size: 0.8rem;
    max-height: 48px;
    position: relative;
    z-index: 1040;
    transition: max-height 0.35s ease, opacity 0.3s ease, padding 0.3s ease;
}
```

### Edit 2 — `.navbar-primary` (lines 62-67)
Give the lower bar an explicit, **lower** z-index so the upper bar (and its dropdowns) sit above it:
```css
.navbar-primary {
    background: rgba(44, 26, 14, 0.55);
    backdrop-filter: blur(10px);
    -webkit-backdrop-filter: blur(10px);
    position: relative;
    z-index: 1030;
    transition: background 0.3s ease, backdrop-filter 0.3s ease;
}
```

### Edit 3 — collapse state `#mainNav.nav-utility-hidden .navbar-utility` (lines 55-59)
Re-add `overflow: hidden` **only** in the hidden state so the scroll-collapse animation still clips the bar cleanly (it was only needed there):
```css
#mainNav.nav-utility-hidden .navbar-utility {
    max-height: 0;
    opacity: 0;
    border-bottom: none;
    overflow: hidden;
}
```

Why this is enough: the dropdown menus are descendants of `.navbar-utility`; raising `.navbar-utility` (z-index 1040) establishes a single stacking context above `.navbar-primary` (1030), and removing `overflow: hidden` stops the clipping. Bootstrap's `.dropdown-menu` default `z-index: 1000` is relative within that context, so it stays above the lower bar.

## Behavior preserved
- Upper-bar hide/show on scroll, blur→solid transition, and two-tier design are unchanged.
- Inner-page solid bars (`body:has(.inner-page-wrapper)`) and `navbar-shrink` rule unaffected.

## Validation
1. **Top of page (home, blurred lower bar, both bars visible):** open language and account dropdowns → fully visible above the lower bar, not clipped.
2. **Scrolled down (solid lower bar, upper bar hidden):** confirm upper bar collapses cleanly (no leftover sliver); reopen after scrolling back to top if needed.
3. **Inner pages** (`.inner-page-wrapper`): repeat step 1.
4. **Mobile/collapsed view:** at < 992px, verify language + account dropdowns still open above content and the lower-bar hamburger menu still works.
5. `dotnet build` compiles (CSS-only change; no build impact expected).
