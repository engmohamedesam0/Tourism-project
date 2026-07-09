# Seed Accounts

These accounts are created from `SeedData/users.json` by `Services/DbInitializer.cs`
on first run (see the JSON Seed Data Plan). Passwords satisfy the 12-character
minimum configured in `Program.cs`.

| Role   | Name            | Email                        | Password         | Notes                              |
|--------|-----------------|------------------------------|------------------|------------------------------------|
| Admin  | Admin User      | admin@egyxplore.com          | `AdminPass123!`  | Full admin access                  |
| Sponsor| El Fishawy     | elfishawy@egyxplore.com      | `SponsorPass123!`| Linked to Sponsor #6 (El Fishawy Cafe) |
| Tourist| Ahmed Hassan   | ahmed.hassan@egyxplore.com   | `TouristPass123!`| Linked to Tourist #1               |

## Additional tourist accounts

All use password `TouristPass123!` and are linked to Tourist rows 2–12:

- james.wilson@egyxplore.com
- sophie.muller@egyxplore.com
- yuki.tanaka@egyxplore.com
- mohamed.ali@egyxplore.com
- emma.johnson@egyxplore.com
- lukas.weber@egyxplore.com
- mei.chen@egyxplore.com
- omar.farouk@egyxplore.com
- hannah.becker@egyxplore.com
- khalid.nasser@egyxplore.com
- sara.rossi@egyxplore.com

## How the data is seeded

1. `TouristContext` no longer uses `modelBuilder.HasData(...)`.
2. `DbInitializer.Initialize(...)` runs once after `app.Build()` in `Program.cs`.
3. It ensures the `Admin`, `User`, and `Sponsor` roles exist, creates the
   accounts above via `UserManager`, then fills every application table from the
   `SeedData/*.json` files in FK-dependency order.
4. Each table is guarded by an `Any()` check, so re-running never duplicates rows.

### Refreshing / resetting the sample data

To replace all sample data with a clean JSON-driven seed, drop and recreate the
database (or delete all rows), then restart the app:

```powershell
dotnet ef database drop --force
dotnet ef database update
# then start the app so DbInitializer re-seeds from JSON
```
