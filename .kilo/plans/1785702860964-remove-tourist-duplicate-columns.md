# Refactor: Remove Duplicated Profile Columns from Tourist Entity

## Status: Investigation Complete — Plan Ready

## Problem Summary

The `Tourist` entity stores `Name`, `Nationality`, `Email`, and `Password` as physical columns that duplicate data already held on `ApplicationUser` (Identity). At registration these are written twice (once to Identity, once to Tourist) and never kept in sync. Additionally `Tourist.Password` stores a plaintext copy — a security defect.

## Current State (verified)

### ApplicationUser (Identity) — custom fields already present
| Field | Type | Notes |
|-------|------|-------|
| `FirstName` | string(100) | collected at signup |
| `LastName` | string(100) | collected at signup |
| `Nationality` | string(100) | collected at signup |
| `ProfilePicturePath` | string?(500) | collected at signup |
| *(inherited)* `Email`, `UserName`, `PasswordHash`, `PhoneNumber` | — | Identity defaults |

### RegisterViewModel — fields collected at signup
`AccountType`, `UserEmail`, `Password`, `ConfirmPassword`, `FirstName`, `LastName`, `PhoneNumber`, `Nationality`, `ProfilePicture`

### Tourist entity — current columns
**Duplicated (to be removed):**
- `Name` (string, required) — derived from `FirstName + LastName`
- `Nationality` (string, required) — exact duplicate of `ApplicationUser.Nationality`
- `Email` (string, required) — exact duplicate of `ApplicationUser.Email`
- `Password` (string, required) — **plaintext leak** of credential; should never exist on Tourist

**Tourist-specific (to be KEPT):**
`IdNumber`, `Passport`, `point_Balance`, `RegisterDate`, `Status`, `PreferredLanguage`, `TravelInterests`, `NotifyByEmail`, `NotifyInApp`, `ApplicationUserId` (FK → AspNetUsers), `TripPlans`, `UserMissions`, `Redemptions`, `UserProgress`, `UserBadges`

### Sponsor pattern (reference for consistency)
`Sponsor` already follows the target pattern: it has its own business-specific columns (`Type`, `Address`, `ContactNumber`, `Name`, `Email`) plus `ApplicationUserId` FK (nullable). The `SponsorApprovalController` already reads shared profile fields (name, email, nationality, phone) from `ApplicationUser`, not from `Sponsor`. **Sponsor will NOT be touched** per task constraint.

### Existing relationship config (already in TouristContext.OnModelCreating)
Tourist → ApplicationUser is a one-to-many (User has many Tourists via `ApplicationUserId` FK, nullable, `DeleteBehavior.NoAction`). This is correct and will be retained.

## Design Decision

**Approach:** Remove the 4 duplicated columns from `Tourist`. Add `[NotMapped]` read-only computed properties on `Tourist` that delegate to the `ApplicationUser` navigation property with null-safe fallbacks. This satisfies requirement (b): "if duplication is unavoidable for a specific field, it should be a computed read-only property, not a directly editable duplicate column."

- `Name` → computed: `$"{ApplicationUser?.FirstName} {ApplicationUser?.LastName}".Trim()` with fallback to `ApplicationUser?.UserName` then `"Unknown"`
- `Email` → computed: `ApplicationUser?.Email ?? string.Empty`
- `Nationality` → computed: `ApplicationUser?.Nationality ?? string.Empty`
- `Password` → **deleted entirely** (no computed property; it is a security defect)

**Critical:** Every query that accesses these computed properties must `.Include(t => t.ApplicationUser)` so the navigation is loaded. Currently most queries do NOT include `ApplicationUser`.

## Implementation Steps (ordered)

### Step 1 — Model: `Models/Tourist.cs`
- Remove stored properties: `Name`, `Nationality`, `Email`, `Password`
- Add `[NotMapped]` computed read-only properties:
  ```csharp
  [NotMapped]
  public string Name => ApplicationUser != null
      ? $"{ApplicationUser.FirstName} {ApplicationUser.LastName}".Trim()
      : (ApplicationUserId != null ? "Unknown" : "Unknown");
  
  [NotMapped]
  public string Email => ApplicationUser?.Email ?? string.Empty;
  
  [NotMapped]
  public string Nationality => ApplicationUser?.Nationality ?? string.Empty;
  ```
- Keep all Tourist-specific fields and the `ApplicationUserId`/`ApplicationUser` navigation as-is.
- Add `using System.ComponentModel.DataAnnotations.Schema;` for `[NotMapped]`.

### Step 2 — Repository: `Repositories/TouristRepository.cs`
- In `GetOrCreateByApplicationUser`, remove the assignments to `Name`, `Email`, `Nationality`, `Password`. The auto-created Tourist should only set: `RegisterDate`, `Status`, `point_Balance = 0`, `ApplicationUserId`.
- In the email self-heal fallback (find by email), the `ApplicationUserId` link still works; no Name/Email/Nationality writes needed.

### Step 3 — Registration flow: `Controllers/AccountController.cs` (`Register` POST)
- **Fix bug:** Remove `PasswordHash = userFromRequest.Password` — `userManager.CreateAsync(user, password)` handles hashing. Setting `PasswordHash` manually to plaintext is a security defect.
- Remove the three lines that set `tourist.Name`, `tourist.Nationality`, `tourist.Email` (lines 115-117). Just create the linked Tourist with `ApplicationUserId` set (which `GetOrCreateByApplicationUser` already does).

### Step 4 — Profile flow: `Controllers/TouristProfileController.cs`
- **Index action:** Source `Name`, `Email`, `Nationality` from `appUserDetails` (ApplicationUser), not from `tourist`. The `TouristProfileVM` already has `FirstName`/`LastName`/`Email`/`Nationality` properties; populate them exclusively from ApplicationUser.
- **Edit GET action:** Source `Email`, `Nationality` from `appUserDetails` exclusively; remove the `?? appUserDetails?.Email ?? string.Empty` fallback.
- **Edit POST action:** Remove `tourist.Nationality = vm.Nationality` and `tourist.Name = ...` lines. Only update tourist-specific fields (`PreferredLanguage`, `TravelInterests`, `NotifyByEmail`, `NotifyInApp`). All name/email/nationality/phone/photo changes go to `appUserDetails` via `UserManager`.

### Step 5 — Admin Tourist list: `Controllers/TouristController.cs`
- **Index action:** Add `.Include(t => t.ApplicationUser)` to `GetAllWithDetails()` in the repository (Step 2), OR add a dedicated include here. Update the search filter and nationality filter to use `t.ApplicationUser`:
  - `t.Name.Contains(search)` → `t.ApplicationUser != null && $"{t.ApplicationUser.FirstName} {t.ApplicationUser.LastName}".Contains(search)`
  - `t.Email.Contains(search)` → `t.ApplicationUser?.Email != null && t.ApplicationUser.Email.Contains(search)`
  - `t.Nationality.Contains(search)` → `t.ApplicationUser?.Nationality`
  - Nationality distinct list: `all.Select(t => t.ApplicationUser?.Nationality ?? "Unknown")`
- **Create POST:** The `Tourist` model no longer has `Name`/`Email`/`Nationality`/`Password`. The binding will ignore those fields. The controller already sets `RegisterDate` and `Status`. No change needed beyond model change.
- **Details action:** Add `.Include(t => t.ApplicationUser)` to `GetByIdWithDetails()` so the Details view can read `@Model.Name`, `@Model.Email`, `@Model.Nationality` via computed properties.

### Step 6 — Repository includes: `Repositories/TouristRepository.cs`
- Add `.Include(t => t.ApplicationUser)` to both `GetAllWithDetails()` and `GetByIdWithDetails()`.

### Step 7 — Admin Dashboard: `Controllers/AdminDashboardController.cs`
- In `BuildTouristSection`, add `.Include(t => t.ApplicationUser)` to the `allTourists` query.
- The `t => t.Nationality` GroupBy now resolves via the computed property (ApplicationUser must be included).
- `TopTouristRow(t.Name, ...)` now resolves via the computed property (ApplicationUser included).

### Step 8 — Admin Support: `Controllers/AdminSupportController.cs`
- In `Index`, change the `touristNames` dictionary from `t.Name` to a join that resolves the name from ApplicationUser:
  ```csharp
  var touristUsers = _context.Users
      .Where(u => allTouristIds.Contains(u.Id))
      .ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
  ```
  Then use `touristUsers` for the name lookup.
- In `Details`, add `.Include(t => t.ApplicationUser)` when fetching the tourist by ID, or fetch the user separately. `tourist?.Name` resolves via computed property if ApplicationUser is loaded.

### Step 9 — NotificationService: `Services/NotificationService.cs`
- Line 40: `redemption.Tourist.Name` → `redemption.Tourist.ApplicationUser != null ? $"{redemption.Tourist.ApplicationUser.FirstName} {redemption.Tourist.ApplicationUser.LastName}".Trim() : "a tourist"`.
- Add `.Include(r => r.Tourist).ThenInclude(t => t.ApplicationUser)` to the query if not already eager-loaded. Check current query — it has `.Include(r => r.Tourist)` but not ApplicationUser.

### Step 10 — AiChatService: `Services/AiChatService.cs`
- Line 766: `tourist.Name` → computed property works IF `ApplicationUser` is loaded. Need to verify the tourist is loaded with ApplicationUser in the AiChatController's `ResolveTouristAsync`. Check: `GetOrCreateByApplicationUser` returns a tracked entity; if ApplicationUser wasn't loaded, the computed property returns "Unknown". **Fix:** Add `.Include(t => t.ApplicationUser)` in `GetOrCreateByApplicationUser` when the Tourist is found by ID/email (before returning).

### Step 11 — DestinationController: `Controllers/DestinationController.cs`
- Line 123: `r.Tourist?.Name` → works via computed property if `r.Tourist.ApplicationUser` is loaded. Add `.Include(r => r.Tourist).ThenInclude(t => t.ApplicationUser)` to the reviews query.

### Step 12 — NearMeController: `Controllers/NearMeController.cs`
- Line 175: `r.Tourist?.Name` → same fix: add `.ThenInclude(t => t.ApplicationUser)`.

### Step 13 — SponsorRedemptionController: `Controllers/SponsorRedemptionController.cs`
- Line 106: `r.Tourist?.Name` → add `.Include(r => r.Tourist).ThenInclude(t => t.ApplicationUser)`.

### Step 14 — TouristProfileVM: no model change
- `TouristProfileVM` already has `Name`, `Email`, `Nationality`, `FirstName`, `LastName`, `ProfilePicturePath` properties. These stay. The controller (Step 4) will populate them from ApplicationUser.

### Step 15 — Views
**No changes needed for most views** if computed properties + Includes are in place:
- `Views/Tourist/Index.cshtml` — `@item.Name`, `@item.Email`, `@item.Nationality` resolve via computed properties (ApplicationUser loaded via `GetAllWithDetails` Include)
- `Views/Tourist/Details.cshtml` — `@Model.Name`, `@Model.Email`, `@Model.Nationality` resolve via computed properties (ApplicationUser loaded via `GetByIdWithDetails` Include)
- `Views/Tourist/Delete.cshtml` — `@Model.Name`, `@Model.Nationality`, `@Model.Email` — **need Include** in the Delete GET action. Currently `_repo.GetById(id)` is used. Either add an Include variant or use `GetByIdWithDetails`.
- `Views/TouristProfile/Index.cshtml` — uses `TouristProfileVM` fields, populated from ApplicationUser (no view change)
- `Views/Trip/Index.cshtml` — `tourist.Name` resolves via computed property (tourist loaded via `GetOrCreateByApplicationUser` which now includes ApplicationUser)
- `Views/AdminDashboard/Sections/_Tourists.cshtml` — `@tourist.Name` where `tourist` is a `TopTouristRow` (VM), populated from controller (no view change)

**Views that DO change:**
- `Views/Tourist/Create.cshtml` — Remove `Name`, `Email`, `Nationality`, `Password` form fields (no longer on model; admin-created Tourists without login won't have these)
- `Views/Tourist/Edit.cshtml` — Remove `Name`, `Email`, `Nationality`, `Password` form fields; keep `IdNumber`, `Passport`, `point_Balance`, `Status`

### Step 16 — Seed data: `SeedData/tourists.json`
- Remove `Name`, `Nationality`, `Email`, `Password` fields from each tourist entry. Keep: `Id`, `IdNumber`, `Passport`, `point_Balance`, `Status`, `RegisterDate`, `ApplicationUserId`.
- Example:
  ```json
  { "Id": 1, "IdNumber": "EG123456789", "Passport": null, "point_Balance": 350, "Status": "Active", "RegisterDate": "2026-01-10", "ApplicationUserId": "seed-tourist-1" }
  ```

### Step 17 — DbInitializer: `Services/DbInitializer.cs`
- No change needed for Tourist seeding — `SeedTableAsync<Tourist>` deserializes JSON into the Tourist entity. Removed fields simply won't be in the JSON.
- `SeedUsersAsync` already creates ApplicationUsers with FirstName/LastName/Nationality — no change needed.

### Step 18 — Migration (hand-authored, since dotnet-ef is not installed)
Create `Migrations/20260802XXXXXX_RemoveTouristDuplicateColumns.cs`:
```csharp
public partial class RemoveTouristDuplicateColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name", table: "Tourists");
        migrationBuilder.DropColumn(name: "Nationality", table: "Tourists");
        migrationBuilder.DropColumn(name: "Email", table: "Tourists");
        migrationBuilder.DropColumn(name: "Password", table: "Tourists");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Name", table: "Tourists", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Nationality", table: "Tourists", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Email", table: "Tourists", type: "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Password", table: "Tourists", type: "text", nullable: false, defaultValue: "");
    }
}
```

### Step 19 — ModelSnapshot: `Migrations/TouristContextModelSnapshot.cs`
Manually update the `Tourist` entity builder in the snapshot to:
- Remove `Name`, `Nationality`, `Email`, `Password` property configurations
- Confirm `ApplicationUserId` FK + relationship config remains

### Step 20 — `TouristController.Delete` GET — add Include
The Delete view reads `@Model.Name`, `@Model.Nationality`, `@Model.Email`. Change `GetById` to a version that includes `ApplicationUser`, or use `_context.Tourists.Include(t => t.ApplicationUser).FirstOrDefault(t => t.Id == id)`.

## Data Migration Consideration

**No manual SQL data migration is needed.** The 4 dropped columns (`Name`, `Nationality`, `Email`, `Password`) are pure duplicates whose authoritative values live on `ApplicationUser`. After the migration drops the columns:
- All reads go through the `[NotMapped]` computed properties, which pull from `ApplicationUser` via the FK.
- The `Password` column contained plaintext passwords — dropping it is a security improvement; no data is lost that wasn't already hashed properly in `AspNetUsers.PasswordHash`.
- Seed data (`tourists.json`) is updated to not include these fields; the linked `users.json` entries already have the authoritative values.

## Files Changed Summary

| File | Change |
|------|--------|
| `Models/Tourist.cs` | Remove 4 stored columns; add 3 `[NotMapped]` computed properties |
| `Repositories/TouristRepository.cs` | Remove Name/Email/Nationality/Password writes; add `.Include(t => t.ApplicationUser)` |
| `Controllers/AccountController.cs` | Remove PasswordHash bug; remove tourist.Name/Nationality/Email writes |
| `Controllers/TouristProfileController.cs` | Source profile fields exclusively from ApplicationUser |
| `Controllers/TouristController.cs` | Update Delete GET to include ApplicationUser; filtering uses ApplicationUser |
| `Controllers/AdminDashboardController.cs` | Add `.Include(t => t.ApplicationUser)` to allTourists |
| `Controllers/AdminSupportController.cs` | Resolve tourist names from ApplicationUser |
| `Services/NotificationService.cs` | Resolve tourist name via ApplicationUser navigation |
| `Services/AiChatService.cs` | Resolve tourist name via ApplicationUser navigation |
| `Controllers/DestinationController.cs` | Add `.ThenInclude(t => t.ApplicationUser)` to reviews query |
| `Controllers/NearMeController.cs` | Add `.ThenInclude(t => t.ApplicationUser)` to reviews query |
| `Controllers/SponsorRedemptionController.cs` | Add `.ThenInclude(t => t.ApplicationUser)` to redemptions query |
| `Views/Tourist/Create.cshtml` | Remove Name, Email, Nationality, Password fields |
| `Views/Tourist/Edit.cshtml` | Remove Name, Email, Nationality, Password fields |
| `SeedData/tourists.json` | Remove Name, Nationality, Email, Password fields |
| `Migrations/20260802XXXXXX_RemoveTouristDuplicateColumns.cs` | NEW — drop 4 columns |
| `Migrations/TouristContextModelSnapshot.cs` | Update Tourist entity config |

## Verification Plan

1. **Build:** `dotnet build` — 0 errors
2. **Migration:** Hand-written migration drops 4 columns; verify `context.Database.Migrate()` in Program.cs applies it on startup (already calls `Migrate()`)
3. **Registration flow:** Register → creates ApplicationUser with FirstName/LastName/Nationality/Email/PhoneNumber/ProfilePicturePath → creates Tourist with only `ApplicationUserId` FK + tourist-specific defaults → verify in DB that Tourists table has no Name/Nationality/Email/Password columns
4. **Profile page:** Verify name, email, nationality, photo display correctly via ApplicationUser
5. **Admin Tourist list:** Verify name, email, nationality display via computed properties; verify search and nationality filter work
6. **Admin Tourist Details:** Verify name, email, nationality display
7. **Tourist Level Badge:** Verify still works (uses UserProgress, not Tourist.Name)
8. **AiChat:** Verify tourist name appears in system prompt via ApplicationUser navigation
9. **Notifications:** Verify redemption notification shows tourist name from ApplicationUser
10. **Seed data:** Verify DbInitializer seeds cleanly with updated tourists.json
11. **PostGIS:** Verify geometry/Point columns on Destination and Branch are unaffected
12. **Playwright tests:** `tests/example.spec.js` only tests the Explore page — no registration/tourist-profile tests exist. Flag: should add registration + profile display tests as a follow-up.

## Risks & Edge Cases

1. **Admin-created Tourists without ApplicationUser:** After removing Name/Email/Nationality, computed properties return fallback values ("Unknown" / empty string). The admin Create/Edit forms no longer let admins set these. If an admin needs to create a Tourist with a name, they must first create an ApplicationUser (via Register or a future admin user-creation flow) and link it.

2. **Lazy loading:** EF Core does not lazy-load by default. Every query accessing `Tourist.Name`/`.Email`/`.Nationality` must have `.Include(t => t.ApplicationUser)`. Missing includes will silently return fallback values. The plan above identifies all query sites.

3. **EF Core translatability:** The `[NotMapped]` computed properties cannot be used in `.Where()` clauses translated to SQL. However, all current filtering on these fields happens after `.ToList()` (LINQ to Objects), so this is not an issue.

4. **Migration timestamp:** Use a timestamp after the existing `20260801165721_init` migration.
