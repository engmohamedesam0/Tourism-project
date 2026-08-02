# Theme Toggle Fix Plan

## Context

The theme toggle at `Views/Shared/_ThemeToggle.cshtml` uses an `<object>` element to load a Lottie-exported SVG (`wwwroot/lottie/Day and night switch button.svg`). Two problems: the SVG's `<animate>/<animateTransform>` elements all have `repeatCount="indefinite"`, so the animation loops forever and ignores clicks; and the CSS adds a circular button chrome (border, background, box-shadow) around the SVG. The toggle logic in `theme-engine.js` also cycles through three states (light → dark → system) instead of a simple two-way flip.

## Files to Modify

1. `Tourist_Project_MVC/wwwroot/css/theme-toggle.css`
2. `Tourist_Project_MVC/wwwroot/lottie/Day and night switch button.svg`
3. `Tourist_Project_MVC/wwwroot/js/theme-engine.js`

---

## Change 1: Remove circular button chrome (theme-toggle.css)

On `.theme-toggle-svg`, remove `border`, `border-radius`, `background`, `box-shadow`, and `transition` properties. Keep `width`, `height`, `cursor`, `position`, `display`, `overflow`, `outline`, `-webkit-tap-highlight-color`, `touch-action`, `flex-shrink`, and `padding`.

Remove the same properties from all variants:
- `.theme-toggle-svg:hover` — remove `border-color`, `box-shadow`, `transform`
- `.theme-toggle-svg:focus-visible` — remove `border-color`, `box-shadow`
- `.theme-toggle-svg:active` — remove `transform`
- `[data-theme="light"] .theme-toggle-svg` — remove `border-color`, `background`, `box-shadow`
- `[data-theme="light"] .theme-toggle-svg:hover` — remove `border-color`, `box-shadow`
- `[data-theme="dark"] .theme-toggle-svg` — remove `border-color`, `background`, `box-shadow`
- `[data-theme="dark"] .theme-toggle-svg:hover` — remove `border-color`, `box-shadow`

Also remove the `@keyframes theme-toggle-pulse` rule and the `.theme-toggle-svg { animation: theme-toggle-pulse 2s ease-in-out 1; }` rule.

Keep the tooltip (`::after`) styles, the `.theme-toggle-svg__animation` and `.theme-toggle-svg__overlay` rules, the RTL adjustment, and the responsive sizing media query unchanged.

---

## Change 2: Make SVG animation respond to clicks

### 2a. Remove `repeatCount="indefinite"` from SVG

In `Day and night switch button.svg`, remove the `repeatCount="indefinite"` attribute from every `<animate>` and `<animateTransform>` element. This prevents the animation from auto-looping. Keep `dur="4.7s"`, `fill="freeze"`, `calcMode="spline"`, `keyTimes`, `keySplines`, and `values` unchanged.

### 2b. Add SVG animation control to theme-engine.js

Add a new module-level function `initSvgAnimation()` that:

1. Waits for the `<object id="themeToggleSvg">` to fire its `load` event.
2. Gets the inner SVG root: `var svg = themeToggleSvg.contentDocument.documentElement`.
3. Calls `svg.pauseAnimations()` immediately to prevent auto-play.
4. Stores a reference to the inner SVG element and its duration (4.7s) in module-level variables.

Add a `setThemeFrame(theme)` function that:

1. Computes the target time in seconds:
   - `'light'` → `0` (keyTimes=0, day settled frame)
   - `'dark'` → `2.3333` (keyTimes=0.496454 * 4.7, night settled frame)
2. Uses `requestAnimationFrame` to smoothly animate `svg.setCurrentTime()` from the current time to the target time over ~500ms.
3. After reaching the target, calls `svg.pauseAnimations()` again to keep it frozen on the settled frame.

Modify the `toggle()` function to call `setThemeFrame(newTheme)` after applying the theme, where `newTheme` is the new light/dark value.

Modify `init()` to call `setThemeFrame(getEffectiveTheme())` after applying the initial theme, so the SVG starts on the correct frame.

### 2c. Handle object load timing

Since the `<object>` loads asynchronously, `initSvgAnimation()` must be called on DOM ready (same place as the existing `init()` call). If the object hasn't fired `load` yet by the time the user clicks toggle, the toggle should still work (just skip the SVG animation seek — the CSS `data-theme` change will still apply).

---

## Change 3: Simplify toggle to two-state light/dark

In `theme-engine.js`:

1. Change the `toggle()` function's cycle from `['light', 'dark', 'system']` to a simple flip between `'light'` and `'dark'`.
2. On each click, if current stored mode is `'light'`, set to `'dark'`; if `'dark'`, set to `'light'`.
3. Remove the `'system'` case from `updateToggleTitle()`.
4. Keep the `prefers-color-scheme` media query listener for initial theme on first visit (no stored preference), but once the user has clicked the toggle once, only alternate between light and dark.
5. `getStoredMode()` still returns `'system'` when no preference is stored, and `getEffectiveTheme()` still resolves `'system'` to the OS preference. But after the first toggle click, the stored value will be `'light'` or `'dark'`, never `'system'` again.

---

## Validation

1. Click the toggle at `/TouristReward` (or any page) and verify:
   - The SVG sun/moon animation smoothly transitions to the correct settled frame.
   - No circular border, background, or box-shadow surrounds the icon.
   - The theme applies correctly (light ↔ dark).
2. Reload the page and verify the SVG starts on the correct frame for the stored theme.
3. Verify the toggle no longer cycles through a "system" state — it only flips light ↔ dark.
4. Verify the `prefers-color-scheme` listener still works for initial theme on first visit.