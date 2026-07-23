# ArcGIS OAuth 2.0 App Authentication Migration Plan

## Current state
- `appsettings.json` `ArcGIS:ApiKey` is already blank (previous migration step).
- `ArcGIS:DestinationsLayerUrl` and `ArcGIS:BranchesLayerUrl` are already populated in `appsettings.json` (from the earlier ArcGIS Online migration).
- `IHttpClientFactory`, `UserSecretsId`, and `AddUserSecrets<Program>()` are already wired up.
- All ArcGIS access in code is driven by config reads; no hardcoded URLs or keys remain outside `appsettings.json`.

## Manual prerequisite (outside repo)
- In the ArcGIS Online org, create **OAuth 2.0 credentials → for app authentication**, scoped to the Destinations and Branches hosted feature layers with **edit privileges**.
- Copy the resulting `client_id` and `client_secret`.

---

## A. Config changes (`appsettings.json`)
In the `"ArcGIS"` section:
- Remove `ApiKey`.
- Add `"ClientId": ""` and `"ClientSecret": ""` (empty placeholders in source control).
- Add `"TokenEndpoint": "https://www.arcgis.com/sharing/rest/oauth2/token"` (keep configurable for ArcGIS Enterprise).
- Keep `DestinationsLayerUrl` and `BranchesLayerUrl` unchanged.

Real values go into user secrets locally:
```bash
dotnet user-secrets set "ArcGIS:ClientId" "..."
dotnet user-secrets set "ArcGIS:ClientSecret" "..."
```
For production, supply via environment variables (`ArcGIS__ClientId`, `ArcGIS__ClientSecret`) or the deployment secret store.

---

## B. New service: `Services/ArcGisAppTokenService.cs`
Create `Services/ArcGisAppTokenService.cs` with interface + implementation:

```csharp
public interface IArcGisAppTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
```

Implementation requirements:
- Inject `IHttpClientFactory`, `IConfiguration`, `ILogger<ArcGisAppTokenService>`.
- Read `ArcGIS:ClientId`, `ArcGIS:ClientSecret`, `ArcGIS:TokenEndpoint` from config.
- POST form-encoded body `grant_type=client_credentials&client_id={ClientId}&client_secret={ClientSecret}&f=json` to the token endpoint using `IHttpClientFactory.CreateClient()`.
- Parse JSON response: expect `access_token` (string) and `expires_in` (int, seconds).
- If response contains an `error` property, throw `InvalidOperationException` with the error detail (never return an empty token silently).
- Cache the token + absolute expiry (`DateTimeOffset.UtcNow.AddSeconds(expires_in)`) in private fields.
- On `GetAccessTokenAsync`, refresh if:
  - no cached token, OR
  - cached token expiry is within **5 minutes** of now.
- Guard refresh with `SemaphoreSlim(1, 1)` so concurrent requests during refresh wait on the same in-flight call rather than firing duplicates.
- Register as **singleton** in `Program.cs`: `builder.Services.AddSingleton<IArcGisAppTokenService, ArcGisAppTokenService>();`
  - `IHttpClientFactory` is safe to resolve from a singleton.

---

## C. Update `Controllers/MapController.cs`
- Inject `IArcGisAppTokenService`.
- Change `GetMapConfig` to `async Task<IActionResult>`.
- Await `tokenService.GetAccessTokenAsync()` and return it as the `apiKey` field in the same JSON shape (`apiKey`, `destinationsLayerUrl`, `branchesLayerUrl`).
- Keep field name `apiKey` — `maps.js` expects `cfg.apiKey`.
- Only the token source changes; the two layer URL config reads stay the same.

---

## D. Update `Services/ArcGISSyncService.cs`
- Inject `IArcGisAppTokenService` alongside existing dependencies.
- Replace the `ApiKey` property (`_config["ArcGIS:ApiKey"]`) with a method that fetches the current token from the token service once per sync call.
- In `SyncDestinationsAsync` and `SyncBranchesAsync`, fetch the token at the top of the try block (before building adds/updates), wrap in try/catch, and on failure log a warning and return early (matching existing `LogWarning` pattern).
- Pass the fetched token to `QueryObjectIdAsync` and the `applyEdits` URLs.
- Remove the old `ApiKey` null/empty guard; the token service throws on misconfiguration, which the sync service catches.

---

## E. Update `Views/Shared/_Layout.cshtml`
Confirmed: the only places expecting `esriConfig.apiKey` pre-set are the inline script (line 23) and `maps.js`'s own `_ensureApiKey(cfg)` call. No other views or scripts reference it.

Action: **Remove the inline script block** (lines 22-24) that sets `var esriConfig = { apiKey: "@Configuration["ArcGIS:ApiKey"]" };`.

Reason: `maps.js` already calls `fetch('/Map/GetMapConfig')` before initializing any map, then imports `@arcgis/core/config.js` and sets `esriConfig.apiKey = cfg.apiKey`. Removing the inline script eliminates the need for a bootstrap module and keeps token minting server-side. No `_Layout` changes needed otherwise.

---

## F. Register service in `Program.cs`
Add:
```csharp
builder.Services.AddSingleton<IArcGisAppTokenService, ArcGisAppTokenService>();
```
Place it after `AddHttpClient()` and before/around the existing `AddScoped<IArcGISSyncService, ArcGISSyncService>()`. No other Program.cs changes required.

---

## G. `ArcGISSyncService` constructor signature change
Current:
```csharp
public ArcGISSyncService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGISSyncService> logger)
```
New:
```csharp
public ArcGISSyncService(IHttpClientFactory clientFactory, IConfiguration config, ILogger<ArcGISSyncService> logger, IArcGisAppTokenService tokenService)
```
Dependency injection will resolve `IArcGisAppTokenService` automatically because it’s registered in `Program.cs`.

---

## Sanity checks
1. **No secret leakage**: `GetMapConfig` JSON still contains only `apiKey`, `destinationsLayerUrl`, `branchesLayerUrl`. `ClientSecret` never appears in responses.
2. **Map rendering**: Pages using `EGYMaps.initWfsMap` or `initLocationPicker` still load basemap + feature layers via the new OAuth access token.
3. **Sync paths**: `DestinationController.Create/Edit` and `SponsorBranchController.Create/Edit` still call `ArcGISSyncService` and complete without `LogWarning` on success.
4. **Token caching**: Add a temporary `LogInformation` inside `ArcGisAppTokenService.GetAccessTokenAsync` showing expiry time; confirm it is returned from cache across requests within its lifetime and only refreshed once when expired.
5. **Build**: Full solution build succeeds.

---

## Flagged risks (out of scope for this prompt, but blocking if ignored)
- **Hardcoded `Referer` headers** in `ArcGISSyncService.cs` (lines 73, 202): `client.DefaultRequestHeaders.Add("Referer", "http://localhost:5217/");` — ArcGIS Online may reject requests if the referrer doesn’t match the OAuth allowed origins. Fix requires a code change (make referrer configurable or derive from request host).
- **No production secret-loading call**: `AddUserSecrets<Program>()` is dev-only. Production must supply `ArcGIS:ClientId`/`ClientSecret` via environment variables or deployment secrets.

---

## Open question
The prompt’s primary prerequisite is having `client_id` and `client_secret` from the ArcGIS Online org. Do you have those two values ready, or is there a blocker in obtaining them?
