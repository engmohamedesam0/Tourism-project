# Migrate Destination & Branch coordinates to PostGIS `Point` geometry + reseed

## Goal
Replace the separate `Lat`/`Long` (`float`) columns on `Destination` and `Branch` with a single `NetTopologySuite.Geometries.Point Location` property (PostGIS `geometry(Point,4326)`). Apply the identical change to both models in one pass, update every reader, and make the Near Me proximity search a real PostGIS spatial query (`ST_Distance`) instead of the hand-rolled Haversine.

## Prerequisites / dependencies
1. **csproj** — add the spatial plugin, version matching the existing base package (`Npgsql.EntityFrameworkCore.PostgreSQL` is `10.0.3`):
   ```xml
   <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite" Version="10.0.3" />
   ```
2. **Program.cs** (`Tourist_Project_MVC/Program.cs:38`) — enable NetTopologySuite on the provider:
   ```csharp
   options.UseNpgsql(builder.Configuration.GetConnectionString("CS"),
       o => o.UseNetTopologySuite());
   ```

## Model changes
- `Models/Destination.cs`: remove `public float Lat` / `public float Long`; add `public NetTopologySuite.Geometries.Point Location { get; set; } = null!;` (add `using NetTopologySuite.Geometries;`).
- `Models/Branch.cs`: same replacement.
- Point is constructed as `new Point(longitude, latitude) { SRID = 4326 }` everywhere (X = longitude, Y = latitude).

## Migration (one migration for both tables)
Generate with `dotnet ef migrations add SpatialLocation`. Then hand-edit the generated `Up()`/`Down()`:
1. Enable PostGIS (must run; otherwise geometry columns fail silently):
   ```csharp
   migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis;");
   ```
2. Add `Location` geometry column on both tables, populate it from existing `Lat`/`Long` **before** dropping them:
   ```csharp
   migrationBuilder.AddColumn<Point>("Destinations", "Location", c => c.HasColumnType("geometry"));
   migrationBuilder.AddColumn<Point>("Branches", "Location", c => c.HasColumnType("geometry"));
   migrationBuilder.Sql("UPDATE \"Destinations\" SET \"Location\" = ST_SetSRID(ST_MakePoint(\"Long\", \"Lat\"), 4326) WHERE \"Lat\" IS NOT NULL AND \"Long\" IS NOT NULL;");
   migrationBuilder.Sql("UPDATE \"Branches\" SET \"Location\" = ST_SetSRID(ST_MakePoint(\"Long\", \"Lat\"), 4326) WHERE \"Lat\" IS NOT NULL AND \"Long\" IS NOT NULL;");
   migrationBuilder.DropColumn("Destinations", "Lat");
   migrationBuilder.DropColumn("Destinations", "Long");
   migrationBuilder.DropColumn("Branches", "Lat");
   migrationBuilder.DropColumn("Branches", "Long");
   ```
   `Down()` reverses: re-add `Lat`/`Long` (`real`), populate via `ST_Y("Location")`/`ST_X("Location")`, drop `Location`.
3. Apply: `dotnet ef database update`. Verify with `SELECT * FROM pg_extension WHERE extname='postgis';` and `\d Destinations` (should show `Location geometry`).

## Seeding changes (idempotent, no duplicate rows)
- Keep the `SeedTableAsync<T>` path for all non-geo tables. Replace the two calls in `DbInitializer.InitializeAsync` (`Tourist_Project_MVC/Services/DbInitializer.cs:52-53`) with a specialized geo seeder:
  ```csharp
  await SeedGeoAsync<Destination>(context, seedDir, "destinations.json",
      (e, el) => e.Location = new Point(el.GetProperty("lng").GetDouble(), el.GetProperty("lat").GetDouble()) { SRID = 4326 });
  await SeedGeoAsync<Branch>(context, seedDir, "branches.json",
      (e, el) => e.Location = new Point(el.GetProperty("lng").GetDouble(), el.GetProperty("lat").GetDouble()) { SRID = 4326 });
  ```
  `SeedGeoAsync<T>` mirrors the existing `SeedTableAsync` (early-out on `set.AnyAsync()`, shared identity-reseed block) but deserializes each row from `JsonElement` so it can read `lat`/`lng` while still mapping every other field via `el.Deserialize<T>()`.
- **SeedData/destinations.json** and **SeedData/branches.json**: rename each row's `"Lat"`→`"lat"` and `"Long"`→`"lng"` (numeric values unchanged — same real Egyptian coordinates). The extra `lat`/`lng` keys are ignored for other tables; only `Destination`/`Branch` consume them.
- (Optional) In `TouristContext.OnModelCreating`, be explicit: `modelBuilder.Entity<Destination>().Property(d => d.Location).HasColumnType("geometry");` and same for `Branch`.

## Code updates — every `Lat`/`Long` reader
**Destination.Lat/Long**
- `Models/Destination.cs` — model change (above).
- `Views/Destination/Details.cshtml:73` — `Lat: @Model.Location.Y | Long: @Model.Location.X`.
- `Views/Destination/Create.cshtml:43-48` and `Views/Destination/Edit.cshtml:56-61` — replace `asp-for="Lat"`/`asp-for="Long"` with plain inputs `name="Lat"`/`name="Long"` (Destination binds directly to the model, which no longer has these), and pre-fill Edit with `value="@Model.Location.Y"` / `value="@Model.Location.X"`.
- `Controllers/DestinationController.cs` — add `double Lat, double Long` parameters to `Create(Destination)` and `Edit(Destination)` POST actions; before `Add`/`Update` set `destination.Location = new Point(Long, Lat) { SRID = 4326 };`.
- `Views/Explore/Index.cshtml:64-65` — `data-lat="@item.Location.Y" data-lng="@item.Location.X"` (item is `Destination`).
- `Views/Trip/Details.cshtml:72-73` — `data-lat="@td.Destination?.Location?.Y" data-lng="@td.Destination?.Location?.X"`.

**Branch.Lat/Long**
- `Models/Branch.cs` — model change (above).
- `Controllers/SponsorBranchController.cs:86-87,114-115,137-138` — set `branch.Location = new Point(vm.Long, vm.Lat) { SRID = 4326 };` instead of `branch.Lat = vm.Lat`.
- `View Model/SponsorBranchVM.cs` — **keep** `Lat`/`Long` (form binding); they feed `Location` in the controller.
- `Views/SponsorBranch/Index.cshtml:40` — `@item.Location.Y, @item.Location.X`.
- `Views/SponsorBranch/Delete.cshtml:23` — `@Model.Location.Y, @Model.Location.X`.
- `Views/SponsorBranch/Edit.cshtml` & `Create.cshtml` — **no change** (bind to `SponsorBranchVM.Lat/Long`).

## Near Me — real spatial query (key upgrade)
File: `Controllers/NearMeController.cs`. Replace the Haversine usage entirely.
- `origin`: `var origin = new Point(selectedDest.Location.X, selectedDest.Location.Y) { SRID = 4326 };` (was `selectedDest.Lat`/`selectedDest.Long` at lines 44-45).
- One spatial SQL projection (real `ST_Distance` on `geography` → accurate km, no manual math):
  ```csharp
  var proximity = await _context.Database.SqlQuery<BranchProximity>(@$"
      SELECT s.""Id"" AS SponsorId,
             MIN(ST_Distance(b.""Location""::geography, {origin}::geography) / 1000.0) AS DistanceKm,
             (ARRAY_AGG(b.""Id"" ORDER BY ST_Distance(b.""Location""::geography, {origin}::geography)))[1] AS NearestBranchId
      FROM ""Sponsors"" s
      JOIN ""Branches"" b ON b.""SponsorId"" = s.""Id""
      GROUP BY s.""Id""").ToListAsync();
  ```
  where `BranchProximity` (`int SponsorId; double DistanceKm; int NearestBranchId;`) is a private record in the controller. The interpolated `origin` becomes a parameter (Npgsql maps `Point`→geometry; the `::geography` cast gives meter-accurate distance).
- Build cards by joining the already-loaded sponsors/branches to `proximity` by `SponsorId`; nearest branch coords come from `sponsor.Branches.First(b => b.Id == row.NearestBranchId).Location` → `card.Lat = nearest.Location.Y; card.Long = nearest.Location.X; card.DistanceKm = row.DistanceKm;`.
- Order (by `DistanceKm`), distance filter (`c.DistanceKm <= maxKm`), and rating sort all use `row.DistanceKm` — correct km values.
- **Delete** the `Haversine` and `ToRad` private methods (no longer used).
- `View Model/NearMeVM.cs` (`SponsorCardVM.Lat`/`Long`) and `Views/NearMe/Index.cshtml:89-90` — **keep** as-is (display coords for the map placeholder; frontend contract unchanged).
- `Views/NearMe/Details.cshtml:10-11,59,63` — `var displayLat = displayBranch?.Location?.Y ?? 0;` / `...Location?.X ?? 0;` (display only; frontend unchanged).

## Validation
1. `dotnet build` succeeds; `dotnet ef migrations add` then `dotnet ef database update` apply cleanly.
2. Confirm PostGIS present: `SELECT extname FROM pg_extension WHERE extname='postgis';` returns a row.
3. Confirm columns: `\d "Destinations"` and `\d "Branches"` show `Location geometry`, no `Lat`/`Long`.
4. Spot-check a row: `SELECT ST_AsText("Location") FROM "Destinations" WHERE "Id"=1;` → `POINT(31.1342 29.9792)`.
5. Run the app; visit Explore, Trip/Details, Near Me (filter by distance + sort), Destination create/edit, Sponsor Branch create/edit — map placeholders and lists behave as before.
6. Re-run app start twice to confirm idempotent seeding (no duplicate Destination/Branch rows).

## Deliverable (expected answers)
- **(a)** Migration `SpatialLocation` applied cleanly; `postgis` extension confirmed enabled.
- **(b)** All `Lat`/`Long` readers updated — listed above per model (Destination: model + Destination Details/Create/Edit views + DestinationController + Explore/Index + Trip/Details; Branch: model + SponsorBranchController + SponsorBranch Index/Delete views; shared: NearMeController + NearMe/Details; VMs/views for map placeholders kept).
- **(c)** **Both** tables: existing `Lat`/`Long` are converted to `Point` in the migration (safe if rows already seeded); for a fresh DB the specialized seeder builds `Point` from `lat`/`lng`. Same approach on both, no data loss.
- **(d)** Yes — Near Me now uses `ST_Distance(... ::geography ...)/1000` (a true PostGIS spatial query) for proximity ordering/filtering and km display; the manual Haversine was removed.

## Risks
- `CREATE EXTENSION postgis` requires superuser/createrole on the target DB; run migration as a role that can create extensions.
- EF migrations run in a transaction; `CREATE EXTENSION` is transactional on modern PostgreSQL (>=9.1) — safe. If the host PG is very old, create the extension manually first.
- `SqlQuery` interpolated `Point` parameter relies on Npgsql NetTopologySuite mapping; verify after applying the package and `UseNetTopologySuite()`.
