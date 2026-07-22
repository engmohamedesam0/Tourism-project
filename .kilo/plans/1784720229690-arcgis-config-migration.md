# ArcGIS Config Migration Plan — Location Platform → ArcGIS Online

## Prerequisite (outside this repo)
1. In the ArcGIS Online organization portal, publish the **Destinations** and **Branches** hosted feature layers (or reuse existing ones in the new org).
2. Generate a new **API key** scoped to those layers.
3. Record the three new values:
   - `NEW_API_KEY`
   - `NEW_DESTINATIONS_URL` (FeatureServer URL, e.g. `https://servicesx.arcgis.com/<org>/arcgis/rest/services/Destinations/FeatureServer`)
   - `NEW_BRANCHES_URL` (same shape for Branches)

---

## Codebase verification (done)
- Confirmed **all** ArcGIS access is driven by exactly three config keys: `ArcGIS:ApiKey`, `ArcGIS:DestinationsLayerUrl`, `ArcGIS:BranchesLayerUrl`.
- **No hardcoded ArcGIS values** outside `appsettings.json`.
- Consumers:
  - `Views/Shared/_Layout.cshtml:23` — reads `ArcGIS:ApiKey` into `esriConfig.apiKey`
  - `Controllers/MapController.cs:20-22` — returns the three keys via `/Map/GetMapConfig`
  - `Services/ArcGISSyncService.cs:32-34` — reads the three keys for sync
  - `wwwroot/js/maps.js` — consumes the endpoint above; no direct config reads
- The sync service uses standard ArcGIS REST API patterns (`/query`, `/applyEdits`, `token=` query parameter, `wkid: 4326`). **No Location-Platform-specific token format or endpoint shape detected.**
- `UserSecretsId` already exists in `.csproj` (`4c2d691e-8534-4f12-9412-8bdfbb90018f`).
- `Program.cs:21` already calls `builder.Configuration.AddUserSecrets<Program>()`.

---

## Implementation tasks

### A. Replace config values in `appsettings.json`
In `appsettings.json`, under `"ArcGIS"`:
- Replace `ApiKey` value with `""`
- Replace `DestinationsLayerUrl` value with `""`
- Replace `BranchesLayerUrl` value with `""`
- **Keep the key names exactly as-is.** Do not rename.

### B. Move secrets out of source control
Run these commands from the project directory to store the real values in .NET User Secrets (already configured):
```bash
dotnet user-secrets set "ArcGIS:ApiKey" "<NEW_API_KEY>"
dotnet user-secrets set "ArcGIS:DestinationsLayerUrl" "<NEW_DESTINATIONS_URL>"
dotnet user-secrets set "ArcGIS:BranchesLayerUrl" "<NEW_BRANCHES_URL>"
```

### C. Production deployment secret loading
The project has no explicit production secret-loading pattern (no `AddEnvironmentVariables()` call with prefixes, no Key Vault). For production, inject these via environment variables (ASP.NET Core maps `ArcGIS__ApiKey` → `ArcGIS:ApiKey` automatically) or the platform's secret store.

---

## Validation
1. Run locally and confirm:
   - Explore / NearMe / Destination detail pages render the map and both feature layers.
   - Creating/editing a Destination or Branch completes without `LogWarning` entries from `ArcGISSyncService`.
2. Verify the old `services8.arcgis.com/...` URLs and old API key no longer appear anywhere in the repo.

---

## Flagged risks (out of scope for this prompt, but blocking if ignored)

1. **Hardcoded `Referer` header** in `ArcGISSyncService.cs` (lines 73, 202):  
   `client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");`  
   ArcGIS Online validates the `Referer` against the API key's allowed origins. This will **fail in production** unless the header is updated to the real deployed origin or removed. Fix requires a code change, not a config change.

2. **No production secret-loading pattern**: `AddUserSecrets<Program>()` is local-dev only. Production must supply the three ArcGIS values via environment variables or a deployment secret store.
