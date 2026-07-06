# Link ApplicationUser ↔ Tourist (fix Trip page resolution)

## Goal
Replace the fragile email-matching stopgap with a real FK from `Tourist` → `ApplicationUser`,
and guarantee every signed-in `"User"` has a usable `Tourist` record (auto-created/linked at
register time and self-healed on first Trip visit). After this, the Trip page no longer shows the
blocking "No Tourist profile is linked" warning.

## Context (verified)
- `Tourist` (Models/Tourist.cs): required `Name, Nationality, Email, Password` (non-null strings),
  `RegisterDate`, `point_Balance`; `Status` is `string?` (default "Active"). No `ApplicationUser` link today.
- `ApplicationUser` (Models/ApplicationUser.cs): empty `IdentityUser` subclass. PK = `Id` (nvarchar(450)).
- `RegisterViewModel` only has `UserName, UserEmail, Password` → auto-created `Tourist` must set
  `Nationality` and `Password` to `string.Empty` (no source field).
- `Repository<T>` (Repositories/Repository.cs) exposes `protected TouristContext _context` + `Add/Update/Save`,
  so `TouristRepository` can implement resolution with data access, consistent with the rest of the app.
- `ITouristRepository` already DI-registered (Program.cs) — no DI change needed.
- `dotnet-ef` is NOT installed → migrations are hand-authored (see prior Rating/Tags migration pattern).
- `TouristController` (admin CRUD) must NOT change. Admin-created Tourists keep `ApplicationUserId = null` (FK nullable).

## Files to change
1. `Models/Tourist.cs` — add nullable FK property.
2. `Data/TouristContext.cs` — configure relationship in `OnModelCreating` (no seed changes).
3. `Repositories/ITouristRepository.cs` — add `GetOrCreateByApplicationUser(ApplicationUser user)`.
4. `Repositories/TouristRepository.cs` — implement it (single source of truth).
5. `Controllers/AccountController.cs` — inject `ITouristRepository`; after successful register, call resolver.
6. `Controllers/TripController.cs` — replace email `ResolveTourist()` with resolver call; remove null/warning path.
7. `Views/Trip/Index.cshtml` — remove blocking warning banner + `if (tourist != null)` guard (resolver is never null).
8. `Migrations/20260707000000_TouristApplicationUserFk.cs` (NEW, hand-written) + update `Migrations/TouristContextModelSnapshot.cs`.

## Migration (NEW, minimal)
`20260707000000_TouristApplicationUserFk`
- `AddColumn<string>("ApplicationUserId", "Tourists", maxLength: 450, nullable: true)`
- `CreateIndex("IX_Tourists_ApplicationUserId", "Tourists", "ApplicationUserId")`
- `AddForeignKey("FK_Tourists_AspNetUsers_ApplicationUserId", "Tourists", "ApplicationUserId",
   "AspNetUsers", "Id", onDelete: ReferentialAction.NoAction)`
- `Down`: drop FK, drop index, drop column.
- Update `TouristContextModelSnapshot.cs`: add the `ApplicationUserId` property (nvarchar(450), nullable),
  the `IX_Tourists_ApplicationUserId` index, and the FK relationship in the bottom relationships block.
  Do NOT alter existing Tourist seed data (column is nullable → seeds remain valid).

## Implementation steps

### 1. Model
`Models/Tourist.cs` add:
```csharp
public string? ApplicationUserId { get; set; }
```
(after `public int Id { get; set; }`).

### 2. Relationship config
In `TouristContext.OnModelCreating`, after the `Destination` config (or near other entities), add:
```csharp
modelBuilder.Entity<Tourist>()
    .Property(t => t.ApplicationUserId)
    .HasMaxLength(450);

modelBuilder.Entity<Tourist>()
    .HasOne<ApplicationUser>()
    .WithMany()
    .HasForeignKey(t => t.ApplicationUserId)
    .OnDelete(DeleteBehavior.NoAction);
```
No changes to `HasData` for Tourists (nullable column).

### 3. Repository resolver (single source of truth)
`ITouristRepository.cs` add:
```csharp
Tourist GetOrCreateByApplicationUser(ApplicationUser user);
```
`TouristRepository.cs` implement (uses `_context.Tourists`):
1. If `user == null || string.IsNullOrWhiteSpace(user.Email)` → create with `ApplicationUserId = user?.Id` (still functional).
2. Find by `ApplicationUserId == user.Id` → return.
3. Else `FirstOrDefault(t => t.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))`:
   - if found: set `ApplicationUserId = user.Id`, `Update`, `Save`, return (self-heal once).
4. Else create new `Tourist`:
   ```csharp
   var t = new Tourist {
       Name = user.UserName ?? user.Email,
       Email = user.Email,
       Nationality = string.Empty,
       Password = string.Empty,
       RegisterDate = DateTime.Now,
       Status = "Active",
       point_Balance = 0,
       ApplicationUserId = user.Id
   };
   Add(t); Save;
   return t;
   ```
Returns `Tourist` (never null).

### 4. Register flow
`AccountController.cs`:
- Add `private readonly ITouristRepository _touristRepo;` + constructor param; assign.
- In `Register` POST, inside `if (identityResult.Succeeded)`:
  ```csharp
  var created = await userManager.FindByNameAsync(applicationUser.UserName);
  _touristRepo.GetOrCreateByApplicationUser(created);
  ```
  (covers step 2: links pre-created Tourist by email, else auto-creates). Keep existing
  `AddToRoleAsync(applicationUser, "User")` and `RedirectToAction("Login")`.

### 5. Trip page
`TripController.cs`:
- Replace private `ResolveTourist()` body with:
  ```csharp
  var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
  return _touristRepo.GetOrCreateByApplicationUser(user);
  ```
  (never null). Remove the `if (tourist == null)` warning branch and the ModelState error about missing Tourist.
- Keep passing `ViewBag.Tourist` and `ViewBag.MyTrips` in both `Index` and the invalid-ModelState re-render.
`Views/Trip/Index.cshtml`:
- Remove the `@if (tourist == null)` warning `<div>`.
- Change `@if (tourist != null) { … builder … }` to always render the builder (resolver guarantees non-null).
- Keep `var tourist = ViewBag.Tourist as Tourist;` usage for the "Planning as …" label.

## Constraints honored
- `TouristController` (admin CRUD) untouched; admin-created Tourists stay `ApplicationUserId = null`.
- Admin accounts/roles untouched; only `"User"` self-service flow affected.
- Migration adds only the nullable FK + index + relationship.

## Validation (end-to-end)
Build: `dotnet build` → 0 errors (pre-existing nullable warnings only).
DB: apply hand-written migration (`Update-Database` / Package Manager Console) so `ApplicationUserId` column exists.

(a) **New registration**: register a brand-new user → after submit, a `Tourist` row exists with
`ApplicationUserId` = new user's Id. Sign in → `/Trip` shows "Planning as <name>", picker + itinerary
work, Save creates a `TripPlan` with `TouristId` = that Tourist. Verify in DB.
(b) **Pre-fix account**: sign in as an existing `"User"` with no linked Tourist → first `/Trip` visit
auto-creates (or email-links) a `Tourist` and persists `ApplicationUserId`; can then plan & save a trip.
Confirm the warning banner never appears.

## Open question (recommended default noted)
Auto-created `Tourist.Nationality` / `Tourist.Password` have no source in `RegisterViewModel`.
Recommended: set both to `string.Empty` (Password unused for login; Nationality can be edited later by admin).
Confirm or specify a different placeholder.
