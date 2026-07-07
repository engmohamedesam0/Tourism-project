# EGYXPLORE — Near Me discovery, Features page, Navbar redesign

Implementation-ready plan. Three separate items; build order: **3 (navbar) → 1 (Near Me) → 2 (Features)**.

## Confirmed decisions (deliverable asks)
- **(a) "near the site he will visit"** → Option **(b): explicit destination picker**. Top of Near Me shows a "Near **[DestinationName]**" indicator + a `<select>` of all `Destination`s; distance to each sponsor is computed (Haversine) from the chosen destination's `Lat`/`Long`. Defaults to the first destination. No "magic" recent-destination tracking.
- **(b) Sponsor role is NOT built.** App only has `Admin` and `User` (UI = "Tourist"). The navbar role label renders "Tourist" for `User`, "Admin" for `Admin`. No Sponsor login/role/auth is added. Flagged explicitly.
- **(c) Language switcher** → **wire ASP.NET Core request localization** (chosen by user). Add `UseRequestLocalization` + `CookieRequestCultureProvider` with cultures `en, ar, es, de, zh`. No `.resx`/translated content yet → all copy stays English until resources are authored later (flagged as separate follow-up).
- Menu representation → **`MenuItem` entity** (recommended): `Id, SponsorId, Name, Price, Description?`. Renders consistently.
- New page name → **"Near Me"** (nav label).

---

## Item 3 — Navbar: two-tier + scroll behavior (DO FIRST)

### Context
`_Layout.cshtml` currently has one `<nav id="mainNav">` with a single role-based `<ul>` (Admin / Tourist / Guest experiences) and a `navbar-shrink` class toggled by `wwwroot/js/scripts.js`. `#mainNav` background colors live in `_Layout`'s `<style>` (`#mainNav`, `#mainNav.navbar-shrink`, etc.). `inner-page-wrapper` has `padding-top:80px`.

### Changes
**`Views/Shared/_Layout.cshtml`** — restructure `<nav id="mainNav">` into two stacked bars:
- **Upper bar (`.navbar-utility`)** — utility row, right-aligned:
  - Role label: `@(User.IsInRole("Admin") ? "Admin" : (User.Identity.IsAuthenticated ? "Tourist" : "Guest"))` in a `.role-badge`.
  - **Language switcher**: a Bootstrap dropdown "🌐 Language" listing English / العربية / Español / Deutsch / 中文. Each item is `<a href="#" data-culture="ar">…</a>`; a tiny inline script sets `document.cookie=".AspNetCore.Culture=c=ar|uic=ar;path=/"; location.reload();` (format consumed by `CookieRequestCultureProvider`).
  - **Site search**: small GET form `action="/Explore" method="get"` with `<input name="search">` + search icon button. Initial scope = searches destinations by name (Explore already filters server-side). Flagged as initial scope, not a full search engine.
  - **Account**: if authenticated → `@User.Identity.Name` + a dropdown with Logout (and, for Admin, the existing Add Role / Assign Roles items); if guest → Sign In / Sign Up links. (Move the existing account dropdown logic here.)
- **Lower bar (`.navbar-primary`)** — primary nav: brand + hamburger toggler + the existing role-based `<ul id="navbarResponsive">` (kept as-is). **Add two links to every experience group**: `Near Me` (`asp-controller="NearMe" asp-action="Index"`) and `Features` (`asp-controller="Features" asp-action="Index"`). (Added to Guest, Tourist, and Admin groups for consistency — they are public pages.)
- Brand + toggler live in the lower bar so the hamburger still toggles `#navbarResponsive`.

**`wwwroot/css/site.css`** — add:
- `.navbar-utility`: thin bar; `transition` for hide/show. When `#mainNav.nav-utility-hidden` → `transform: translateY(-100%); opacity:0; pointer-events:none;`.
- `#mainNav` lower bar default: `background: rgba(44,26,14,0.55); backdrop-filter: blur(10px);` (translucent over hero). When `#mainNav.navbar-shrink` (scrolled) → `background: var(--egy-dark); backdrop-filter:none;` (solid). Keep existing gold border.
- `.role-badge`, `.lang-switcher` dropdown styling reusing `--egy-primary`/`--egy-light`.
- Increase top offset for two-tier height: bump `.inner-page-wrapper { padding-top: 140px; }` (was 80px) and `.explore-page { padding-top: 140px; }` (was 90px) and any other fixed `padding-top` that assumes the old single nav.
- **Mobile**: ensure `.navbar-utility` wraps/hides gracefully (e.g., hide the search input on `xs`, keep icons); hamburger in lower bar still collapses `#navbarResponsive` cleanly (existing `scripts.js` toggler logic stays).

**`wwwroot/js/scripts.js`** — extend (do NOT add a second scroll listener): modify the existing `navbarShrink`/scroll handler to be **direction-aware with a threshold (~10px)** and toggle two classes on `#mainNav`:
  - scrolling **down** past threshold → add `navbar-shrink` (lower bar solid) + `nav-utility-hidden` (upper bar slides/fades out).
  - scrolling **up** → remove both (upper reappears, lower returns to translucent).
  - `scrollY === 0` → remove both (top state).
  Keep `navbarShrink()` as the single `scroll` listener (reuse pattern).

**`Program.cs`** — wire localization (for the switcher in (c)):
- `builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");`
- `builder.Services.Configure<RequestLocalizationOptions>(o => { o.SetDefaultCulture("en"); o.AddSupportedCultures("en","ar","es","de","zh"); o.AddSupportedUICultures("en","ar","es","de","zh"); o.RequestCultureProviders = new[] { new CookieRequestCultureProvider() }; });`
- `app.UseRequestLocalization();` placed **after** `app.UseRouting()` (and before `UseAuthorization`).
- No `.resx` files created → copy remains English. Flagged.

**Files:** `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/scripts.js`, `Program.cs`. (Admin CRUD controllers/views untouched beyond navbar links.)

---

## Item 1 — "Near Me" sponsor discovery (schema + pages)

### Schema / migration (minimal, isolated)
- **`Models/Sponsor.cs`**: add `public float Lat { get; set; }` and `public float Long { get; set; }` (mirror `Destination`).
- **New `Models/Review.cs`**: `Id, int Rating, string? Comment, int TouristId, int SponsorId, DateTime CreatedDate`. FKs to `Tourist` and `Sponsor` (`List<Review>? Reviews` on `Sponsor`).
- **New `Models/MenuItem.cs`**: `Id, int SponsorId, string Name, decimal Price, string? Description`. FK to `Sponsor` (`List<MenuItem>? MenuItems` on `Sponsor`).
- **`Data/TouristContext.cs`**: add `DbSet<Review>` + `DbSet<MenuItem>`; `modelBuilder.Entity<MenuItem>().Property(m => m.Price).HasColumnType("decimal(10,2)")`; extend existing `Sponsor` seed with `Lat`/`Long` (near each sponsor's city: Cairo/Luxor/Aswan plausible coords); seed a few `MenuItems` and `Reviews` (using existing Sponsor ids 1–5 and Tourist ids 1–5) so the page isn't empty. Tag all seed additions clearly.
- **Migration**: `dotnet ef migrations add SponsorDiscovery` (+ Designer + auto-updated snapshot). `dotnet ef database update`.

### Repositories / data access
- New `NearMeController` uses `TouristContext` directly for `Sponsor`/`Review`/`MenuItem` (no `ISponsorRepository` exists; keep admin untouched) and `IDestinationRepository` for the destination picker. (Lightweight; matches current access style.)

### Controller — new `Controllers/NearMeController.cs`
- `Index(int? destinationId, string? search, string? type, string? sort)` `[AllowAnonymous]`:
  - Load destinations → `SelectListItem` list for the picker.
  - `selectedDest = destinationId ?? firstDestinationId`; read its `Lat`/`Long`.
  - Load sponsors `Include(Rewards).Include(MenuItems).Include(Reviews)`.
  - Compute distance per sponsor (Haversine vs selected destination).
  - Server-side filter: `search` (name contains), `type` (== Sponsor.Type), rating (avg review ≥ threshold), distance (`Within 5/10/25 km`).
  - `sort`: `distance` (default) or `rating`.
  - Model: `NearMeIndexVM { int? DestinationId; List<SelectListItem> Destinations; string? Search; string? Type; string? Sort; List<SponsorCardVM> Cards }` where `SponsorCardVM` = `Sponsor + DistanceKm + AvgRating + ReviewCount`.
  - ViewBag carries selected values for chip/input state.
- `Details(int id)` `[AllowAnonymous]`: load sponsor `Include(Reviews).Include(MenuItems).Include(Rewards)`; compute avg rating; pass to view. Header shows name/type/address/contact; `#sponsorMap` placeholder with `data-lat`/`data-lng`/`data-id`; reviews list; "Add Review" form (rendered only for `User` role); menu list; linked rewards from this sponsor.
- `AddReview(int id, Review vm)` `[HttpPost][Authorize(Roles="User")][ValidateAntiForgeryToken]`: resolve tourist (`ITouristRepository.GetOrCreateByApplicationUser`), set `SponsorId=id, TouristId, CreatedDate=now`, add + save, redirect to `Details`.

### ViewModels — new `View Model/NearMeVM.cs`
`NearMeIndexVM`, `SponsorCardVM` (above).

### Views
- **`Views/NearMe/Index.cshtml`**: two-panel layout (left list / right `#nearbyMap` placeholder with `data-lat`/`data-lng`/`data-id` per sponsor, sized like Explore/Trip). Top: "Near **[DestinationName]**" + destination `<select>` (GET to `Index`) + search input + filter chips for `Type` (Cafe/Restaurant/…), rating, and distance — re-GET with query params (same chip UX as Explore). Sponsor cards: name, type badge, "X km away", avg rating stars, link to `Details`.
- **`Views/NearMe/Details.cshtml`**: header; `#sponsorMap` placeholder; rating summary + reviews list (tourist name, stars, comment, date); `Add Review` form (rating 1–5 select + comment, antiforgery) shown only to `User`; menu section (items w/ name, price, description); rewards from this sponsor (cross-link to existing `Reward` model).
- **`Views/Shared/_Layout.cshtml`**: add `Near Me` link (done in Item 3).

**Files:** `Models/Sponsor.cs`, `Models/Review.cs` (new), `Models/MenuItem.cs` (new), `Data/TouristContext.cs`, migration + snapshot, `Controllers/NearMeController.cs` (new), `View Model/NearMeVM.cs` (new), `Views/NearMe/Index.cshtml` (new), `Views/NearMe/Details.cshtml` (new).

---

## Item 2 — Features (mobile app marketing) page

### Controller — new `Controllers/FeaturesController.cs`
- `Index()` → returns `Views/Features/Index.cshtml` (static content; no model needed, or a small list of 7 feature descriptors if preferred). Add `Near Me`/`Features` already in nav from Item 3.

### View — new `Views/Features/Index.cshtml`
- **Top CTA**: App Store + Google Play badge buttons (placeholder `href="#"` links, marked `<!-- TODO: real store listings -->`), plus a heading.
- **7 alternating sections** (image/video left↔right via Bootstrap `order-lg-first`, matching Home's feature rows):
  1. Indoor Maps, 2. Navigation Mode, 3. Fun Games, 4. Camera Scanner, 5. AI Assistant, 6. P2P Translation, 7. Rewarding System (copy references the existing `Reward`/`Sponsor` models — "discounts on meals, etc.").
  - Each section: feature name, description, and a media block. Use `<video autoplay loop muted playsinline poster="~/assets/img/...jpg">` with a `<source>` pointing to a placeholder (or, until real footage exists, a poster image + `<!-- TODO: replace with real capture -->`). Reuse existing placeholder images (`ipad.png`, `demo-image-01.jpg`, `demo-image-02.jpg`, `bg-masthead.jpg`).
- **Autoplay-on-scroll**: a `<script>` using `IntersectionObserver` (threshold ~0.5) that, when a section's `<video>` is ≥50% visible, adds `.playing` and calls `.play()`; when it leaves, pauses (so only on-screen video plays). Provide poster fallback.
- **Bottom CTA**: repeat App Store / Google Play badges.
- Styles: keep within existing design system (`.featured-text`, `.project`, gold accents, `--egy-primary`).

**Files:** `Controllers/FeaturesController.cs` (new), `Views/Features/Index.cshtml` (new), (styles inline in view `@section Scripts` or `site.css`).

---

## Build / migration order
1. Item 3: `_Layout`, `site.css`, `scripts.js`, `Program.cs` (localization). Build.
2. Item 1: models + `TouristContext` seed + `dotnet ef migrations add SponsorDiscovery` + `database update`; `NearMeController`, VM, views. Build.
3. Item 2: `FeaturesController` + view. Build.

## Validation
- `dotnet build` → 0 errors; `dotnet ef database update` applies new tables/columns.
- Smoke (app running): `/NearMe` (guest) → 200; `/NearMe?destinationId=1` lists sponsors with computed "X km away"; filter chips + search re-GET correctly; `/NearMe/Details/1` shows reviews + menu; POST `AddReview` as a tourist persists (verify DB row).
- `/Features` → 200; 7 alternating sections; videos play only when scrolled into view (observer); App Store/Play badges present (placeholder links).
- Navbar: two-tier renders for Guest/Tourist/Admin; role label shows "Tourist"/"Admin"; language dropdown sets `.AspNetCore.Culture` cookie and reloads; site search submits to `/Explore?search=`; scroll down hides upper bar + solidifies lower bar, scroll up restores; mobile hamburger still toggles nav.

## Open questions / flags
- Sponsor auth role intentionally **not** built (per constraint). Role label = Tourist/Admin only.
- Language switcher wires middleware + cookie; **no translated copy yet** (English only until `.resx` added later).
- Site search initial scope = destination name search via `/Explore`.
- `NearMe` list/details are browsable by guests; only posting a review requires the `User` role.
- Menu rendered as `MenuItem` rows (not an uploaded image/file).
