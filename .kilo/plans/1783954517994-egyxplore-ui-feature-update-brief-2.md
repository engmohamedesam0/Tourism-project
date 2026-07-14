# EGYXPLORE — UI/Feature Update Task Brief #2 — Implementation Plan

## Execution Order
1. Navbar restyle (CSS only)
2. Footer sitemap (HTML/CSS only)
3. Tourist-facing reviews carousel (DB + backend + partial + view injections)
4. My Profile page overhaul (DB + backend + view)

---

## Item 1 — Restyle the Navbar

### Files to change
- `wwwroot/css/site.css` (lines ~60–220)
- `Views/Shared/_Layout.cshtml` (lines ~255–540) — only if markup adjustments are needed for spacing/classes

### Tasks
1. **Pill-shaped action buttons in utility bar**
   - `.utility-link` (Sign In): keep current text style but add `border-radius: 999px`, `padding: 6px 16px`, `border: 1px solid var(--egy-primary)`, `color: #fff`.
   - `.utility-register` (Sign Up): change to filled pill — `background: var(--egy-accent)`, `color: var(--egy-dark) !important`, `border: none`, `border-radius: 999px`, `padding: 6px 16px`, `font-weight: 700`.
   - Remove the old `.utility-register` border/outline hover logic; make hover just darken slightly (`background: var(--egy-primary)`).

2. **Primary nav link spacing**
   - `#mainNav .nav-link`: increase `letter-spacing` from `1px` to `1.5px`, increase horizontal `padding` from `8px 16px` to `10px 22px`.
   - Add `gap: 6px` to the `.navbar-nav` or increase the `nav-spacer` margin to give more air between links.

3. **Logo spacing**
   - Ensure `.navbar-brand` has `margin-right: 1.5rem` (or equivalent) so the logo stays clearly left-separated from the first nav link.

4. **Verify inner-page overrides**
   - Confirm `body:has(.inner-page-wrapper) / .explore-page / .nearme-page` solid-background rules (~lines 106–121) still work after button/spacing changes. No JS changes.

---

## Item 2 — Reusable "Tourer Reviews" Carousel

### 2a. Backend: New `SiteReview` Model + Migration + Repository

1. **Add `Models/SiteReview.cs`**
   ```csharp
   public class SiteReview
   {
       public int Id { get; set; }
       [Range(1, 5)] public int Rating { get; set; }
       public string? Comment { get; set; }
       public DateTime CreatedDate { get; set; } = DateTime.Now;

       public int TouristId { get; set; }
       public Tourist? Tourist { get; set; }

       public int? DestinationId { get; set; }
       public Destination? Destination { get; set; }
       public int? TripPlanId { get; set; }
       public TripPlan? TripPlan { get; set; }
       public int? RewardId { get; set; }
       public Reward? Reward { get; set; }
       public int? BranchId { get; set; }
       public Branch? Branch { get; set; }
   }
   ```

2. **Register `DbSet<SiteReview>` in `Data/TouristContext.cs`**

3. **Add EF Core migration**
   - Run `dotnet ef migrations add AddSiteReview` (or equivalent timestamped name following `20260711122605_mig2.cs` pattern).
   - Migration must create `SiteReviews` table with nullable FK columns for `DestinationId`, `TripPlanId`, `RewardId`, `BranchId`, plus required `TouristId`.
   - Update `TouristContextModelSnapshot.cs`.

4. **Add repository**
   - `Repositories/ISiteReviewRepository.cs`: `GetForDestination(int destinationId, int take, int skip)`, `GetForTripPlan(...)`, `GetForReward(...)`, `GetForBranch(...)`, `Add(SiteReview review)`, `GetCountForDestination(...)` etc.
   - `Repositories/SiteReviewRepository.cs`: implement using `_context.SiteReviews` with `.Include(r => r.Tourist)`.

5. **Register in `Program.cs`**
   - `builder.Services.AddScoped<ISiteReviewRepository, SiteReviewRepository>();`

### 2b. Review POST Endpoints

Add to each controller (all `[Authorize(Roles = "User")]` except NearMe which already allows anonymous GET):

- **`DestinationController`**: `[HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AddReview(int id, [Bind("Rating,Comment")] SiteReview vm)` — resolves tourist via `User.FindFirst(ClaimTypes.NameIdentifier)`, sets `DestinationId = id`, saves.
- **`TripController`**: `[HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AddReview(int id, ...)` — sets `TripPlanId = id`.
- **`TouristRewardController`**: `[HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AddReview(int id, ...)` — sets `RewardId = id`.
- **`NearMeController`**: For NearMe, **reuse the existing `Review` model** (Sponsor-level) in the carousel. Do NOT create a separate `SiteReview` Branch endpoint for NearMe. The existing `NearMe/Details.cshtml` already has the Sponsor review form. For `NearMe/Index.cshtml`, render the carousel using the existing `Sponsor.Reviews` collection. This preserves existing data and avoids duplication.

### 2c. Reusable Partial: `Views/Shared/_ReviewsCarousel.cshtml`

**View Model:** `View_Model/ReviewsCarouselVM.cs`
```csharp
public class ReviewsCarouselVM
{
    public string Title { get; set; } = "Traveler Reviews";
    public string TargetTitle { get; set; } = string.Empty; // e.g. destination name
    public List<ReviewsCarouselItemVM> Items { get; set; } = new();
    public bool CanAddReview { get; set; } // whether to show the "Add review" form
    public int TargetId { get; set; }
    public string TargetType { get; set; } = string.Empty; // "Destination", "TripPlan", "Reward"
}
public class ReviewsCarouselItemVM
{
    public string TouristName { get; set; } = string.Empty;
    public string? TouristPhotoPath { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }
}
```

**Partial markup:**
- Heading: `<h4 class="mb-3">@Model.Title — @Model.TargetTitle</h4>`
- Bootstrap Carousel (`id="reviewsCarousel-@TargetId"`) with `.carousel-inner`, one `.carousel-item` per review.
- Each slide: large centered quote (comment), star rating row (`bi-star-fill` / `bi-star`), reviewer avatar/photo + name + date.
- Prev/next arrows and dot indicators.
- "Show more reviews" button below carousel: toggles a hidden `.reviews-grid` div that shows remaining reviews as cards (fetch up to 5 for carousel, rest expand in place).

### 2d. Inject Partial into Views

Update each view's controller action to load reviews and pass the partial model:

1. **`Trip/Details.cshtml`** + `TripController.Details(int id)`
   - Load `SiteReview` where `TripPlanId == id`, take 5 for carousel.
   - At bottom of view: `@await Html.PartialAsync("_ReviewsCarousel", carouselVm)`

2. **`TouristReward/Index.cshtml`** + `TouristRewardController.Index()`
   - For each reward card? No — the brief says render near the bottom of the page. But `Index` shows a list of rewards, not a single reward. 
   - **Decision**: Render a single carousel at the bottom of `TouristReward/Index` showing reviews for ALL rewards (or the most-reviewed ones). Alternatively, since `Index` is a list page, add the carousel only when a specific reward is being viewed. But there's no `TouristReward/Details` page.
   - **Revised decision**: The `TouristReward/Index.cshtml` is the "Points & Rewards" page. Since there's no detail page for a single reward, render the carousel for the **most recently reviewed reward** or aggregate all reward reviews under "Reward Reviews". To keep it simple and useful: load all `SiteReview` where `RewardId != null`, group by `RewardId`, and render the carousel for the reward with the most reviews (or the first available reward). Pass `TargetTitle = reward.Title`.
   - Actually, simpler: just show reviews for ALL rewards on the page in a single carousel, with the reward title as context.

3. **`Destination/Details.cshtml`** + `DestinationController.Details(int id)`
   - Load `SiteReview` where `DestinationId == id`, take 5.
   - Inject partial before the closing `</div>` of the card (after card footer).

4. **`NearMe/Index.cshtml`** + `NearMeController.Index(...)`
   - NearMe shows multiple sponsors. For simplicity, render the carousel using the **first sponsor's reviews** or aggregate. Better: since NearMe is a discovery page with many sponsors, skip the carousel here OR render a carousel for the first sponsor card. 
   - **Decision**: The brief explicitly says to include it in NearMe. Render a carousel for the **first sponsor** (or the nearest sponsor) using existing `Sponsor.Reviews`. This keeps it consistent with the existing Sponsor review data. If there are no sponsors/reviews, hide the section.

---

## Item 3 — Footer: Full Sitemap Section

### Files to change
- `Views/Shared/_Layout.cshtml` (lines 626–633)
- `wwwroot/css/site.css` (lines 382–395 for footer base, add new rules)

### Tasks
1. **Replace footer markup** with multi-column layout:
   ```html
   <footer class="main-footer">
       <div class="container">
           <div class="row g-4 mb-4">
               <div class="col-6 col-md-3">
                   <h6 class="footer-col-header">EXPLORE</h6>
                   <ul class="list-unstyled mb-0">
                       <li><a asp-controller="Explore" asp-action="Index">Explore Egypt</a></li>
                       <li><a asp-controller="NearMe" asp-action="Index">Near Me</a></li>
                       <li><a asp-controller="Trip" asp-action="Index">Plan Your Trip</a></li>
                   </ul>
               </div>
               <div class="col-6 col-md-3">
                   <h6 class="footer-col-header">ACCOUNT</h6>
                   <ul class="list-unstyled mb-0">
                       <li><a asp-controller="TouristProfile" asp-action="Index">My Profile</a></li>
                       <li><a asp-controller="TouristReward" asp-action="Index">Points & Rewards</a></li>
                       <li><a asp-controller="Account" asp-action="Login">Sign In</a></li>
                       <li><a asp-controller="Account" asp-action="Register">Sign Up</a></li>
                   </ul>
               </div>
               <div class="col-6 col-md-3">
                   <h6 class="footer-col-header">SUPPORT</h6>
                   <ul class="list-unstyled mb-0">
                       <li><a asp-controller="TouristSupport" asp-action="Index">Support</a></li>
                   </ul>
               </div>
               <div class="col-6 col-md-3">
                   <h6 class="footer-col-header">COMPANY</h6>
                   <ul class="list-unstyled mb-0">
                       <li><a asp-controller="About" asp-action="Index">About Us</a></li>
                       <li><a asp-controller="Features" asp-action="Index">Features</a></li>
                   </ul>
               </div>
           </div>
           <hr class="footer-divider" />
           <div class="text-center">
               <div class="footer-logo mb-2">🏺 EGYXPLORE</div>
               <p class="mb-0 small text-muted">&copy; @DateTime.Now.Year — @Localizer["Footer_Copyright"].Value</p>
           </div>
       </div>
   </footer>
   ```

2. **Add CSS** in `site.css`:
   - `.footer-col-header`: uppercase, gold (`var(--egy-accent)`), letter-spacing `2px`, font-family `Cinzel`, font-size `0.85rem`, margin-bottom `1rem`.
   - `.main-footer a`: color `rgba(255,255,255,0.75)`, text-decoration none, font-size `0.85rem`.
   - `.main-footer a:hover`: color `var(--egy-accent)`.
   - `.footer-divider`: border-color `rgba(200,131,42,0.25)`.
   - Responsive: columns stack on mobile (already handled by Bootstrap grid classes `col-6 col-md-3`).

---

## Item 4 — "My Profile" Page Overhaul

### 4a. New Tourist Fields + Migration

1. **Update `Models/Tourist.cs`**:
   ```csharp
   public string? PreferredLanguage { get; set; }
   public string? TravelInterests { get; set; }
   public bool NotifyByEmail { get; set; } = true;
   public bool NotifyInApp { get; set; } = true;
   ```

2. **Add EF Core migration** (`dotnet ef migrations add AddTouristProfileFields`).
   - Add 4 columns to `Tourists` table.
   - Update ModelSnapshot.

### 4b. "Current Level / Badge" (computed, no DB change)

- In `TouristProfileController.Index()`, compute:
  - 0–499 → "Bronze Explorer"
  - 500–1,999 → "Silver Voyager"
  - 2,000–4,999 → "Gold Pioneer"
  - 5,000+ → "Legendary Pharaoh"
- Add `LevelLabel` and `LevelIcon` (emoji or Bootstrap icon) to `TouristProfileVM`.

### 4c. History Section (derived from existing relationships)

In `TouristProfileController.Index()`, enrich the VM:

```csharp
// Missions Completed
var missionsCompleted = _context.UserMissions
    .Include(um => um.Mission)
    .Where(um => um.TouristId == tourist.Id && um.Status == "Completed")
    .ToList();
int missionsCount = missionsCompleted.Count;
var missionTitles = missionsCompleted.Select(um => um.Mission?.Title).Where(t => !string.IsNullOrEmpty(t)).Take(5).ToList();

// Places Visited: distinct destinations from completed missions + trip plans
var visitedFromMissions = _context.UserMissions
    .Include(um => um.Mission)
    .Where(um => um.TouristId == tourist.Id && um.Status == "Completed")
    .Select(um => um.Mission!.DestinationId)
    .Distinct()
    .ToList();

var visitedFromTrips = _context.TripPlans
    .Include(tp => tp.TripDestinations)
    .Where(tp => tp.TouristId == tourist.Id && tp.Status == "Completed")
    .SelectMany(tp => tp.TripDestinations)
    .Select(td => td.DestinationId)
    .Distinct()
    .ToList();

var allVisitedIds = visitedFromMissions.Union(visitedFromTrips).Distinct().ToList();
int placesVisitedCount = allVisitedIds.Count;
var visitedDestinations = _context.Destinations
    .Where(d => allVisitedIds.Contains(d.Id))
    .Select(d => d.Name)
    .ToList();

// Rewards Redeemed
var redemptions = _context.Redemptions
    .Include(r => r.Reward)
    .Where(r => r.TouristId == tourist.Id)
    .OrderByDescending(r => r.RedemptionDate)
    .ToList();
int rewardsRedeemedCount = redemptions.Count;

// Favorite Destination: most frequent destination across completed missions + trips
var favCounts = new Dictionary<int, int>();
foreach (var dId in visitedFromMissions.Union(visitedFromTrips))
{
    if (!favCounts.ContainsKey(dId)) favCounts[dId] = 0;
    favCounts[dId]++;
}
var favoriteDestId = favCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
var favoriteDestName = favoriteDestId != 0
    ? _context.Destinations.FirstOrDefault(d => d.Id == favoriteDestId)?.Name
    : "—";
```

Add these to `TouristProfileVM`:
```csharp
public string LevelLabel { get; set; } = string.Empty;
public int MissionsCompletedCount { get; set; }
public int PlacesVisitedCount { get; set; }
public int RewardsRedeemedCount { get; set; }
public string? FavoriteDestination { get; set; }
public List<string>? RecentMissionTitles { get; set; }
public List<string>? VisitedDestinationNames { get; set; }
public List<string>? RecentRedemptionTitles { get; set; }
```

### 4d. Quick Actions: Edit Profile + Change Password

1. **New `View_Model/EditTouristProfileVM.cs`**:
   ```csharp
   public class EditTouristProfileVM
   {
       public string? ProfilePicturePath { get; set; }
       public IFormFile? ProfilePicture { get; set; }
       public string FirstName { get; set; } = string.Empty;
       public string LastName { get; set; } = string.Empty;
       public string Email { get; set; } = string.Empty;
       public string PhoneNumber { get; set; } = string.Empty;
       public string Nationality { get; set; } = string.Empty;
       public string? PreferredLanguage { get; set; }
       public string? TravelInterests { get; set; }
       public bool NotifyByEmail { get; set; }
       public bool NotifyInApp { get; set; }
   }
   ```

2. **`TouristProfileController.Edit()` GET/POST**:
   - GET: populate VM from current user/tourist.
   - POST: validate, handle profile picture upload (same pattern as `AccountController.Register` lines 42–68: validate image, max 2MB, save to `wwwroot/uploads/profile-pictures/{guid}.{ext}`, update `ApplicationUser.ProfilePicturePath`).
   - Update `ApplicationUser` fields via `_context.Users.Update(...)` + `_context.SaveChanges()` (FirstName, LastName, PhoneNumber, ProfilePicturePath, Nationality).
   - Update email via `UserManager.SetEmailAsync(user, vm.Email)` and `SetUserNameAsync(user, vm.Email)`.
   - Update `Tourist` fields: PreferredLanguage, TravelInterests, NotifyByEmail, NotifyInApp, Nationality.
   - Use `[Authorize(Roles = "User")]`.

3. **`AccountController.ChangePassword()` GET/POST**:
   - New VM: `ChangePasswordVM` with `CurrentPassword`, `NewPassword`, `ConfirmNewPassword`.
   - GET: return view.
   - POST: `[Authorize]`, resolve user, call `UserManager.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword)`.
   - On success, sign in again or redirect with TempData success.

4. **Modals in `TouristProfile/Index.cshtml`**:
   - Add two Bootstrap modals at the bottom of the page: `#editProfileModal` and `#changePasswordModal`.
   - Each modal contains a form that posts to the respective action.
   - "Edit Profile" and "Change Password" buttons in the Quick Actions card open the modals via `data-bs-toggle="modal"`.

### 4e. Layout Reorganization

Reorganize `TouristProfile/Index.cshtml` into 4 sections:

1. **Top Identity Card** (existing left column, enhanced):
   - Photo, name, email, plus Level/Badge pill next to name.

2. **Details Card** (existing right column, expanded):
   - Existing fields (Full Name, Nationality, Email, Phone, Registration Date, Status, Points Balance).
   - New fields: Preferred Language, Travel Interests (as pills/tags), Notification Preferences (two toggle switches: Email + In-App).

3. **History Section** (new, below identity row):
   - 4 stat-style tiles using existing `.stat-card` pattern (from `_Layout.cshtml` inline styles):
     - Places Visited
     - Rewards Redeemed
     - Missions Completed
     - Favorite Destination
   - Use `_StatBoxRow.cshtml` pattern or inline `.row.g-3` with `.stat-card`.

4. **Quick Actions Card** (new, below history):
   - Two buttons: "Edit Profile" → opens modal, "Change Password" → opens modal.

---

## Validation & Rollout

- After each item, run `dotnet build` and verify the affected pages render.
- After migrations, confirm DB schema updates (do not hand-edit DB).
- Reuse existing patterns: repository pattern, `ClaimTypes.NameIdentifier` lookup, comma-separated tags, upload validation, EF Core migrations.
- Keep all brand colors (`--egy-*` tokens) and fonts (`Cinzel`/`Nunito`). rooom screenshots are layout/interaction reference only.

---

## Open Questions / Decisions (resolved)

1. **NearMe reviews**: Use existing `Review` model (Sponsor-level) for NearMe carousel. Do NOT duplicate into `SiteReview` for NearMe. Preserves existing data.
2. **Favorite Destination**: Derived algorithmically from completed missions + completed trip destinations (most frequent). No new "favorite" UI flag.
3. **TravelInterests**: Comma-separated string, reuse 7 checkboxes from `Trip/Index.cshtml`.
4. **PreferredLanguage default**: Read from `.AspNetCore.Culture` cookie in Edit POST if empty, else default `"en"`.
5. **TouristReward carousel target**: Aggregate all reward reviews into a single carousel at the bottom of `TouristReward/Index`, titled after the most-reviewed reward.
