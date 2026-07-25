# Fix: Wikipedia API 403 — missing User-Agent header

## Problem
`SeedDestinationPhotosAsync` calls `httpClient.GetAsync(url)` with no `User-Agent` header. Wikimedia blocks requests that don't identify the caller, returning HTTP 403 regardless of URL validity.

## Fix (single-target, per-request header — preferred)
Avoid touching the shared default `HttpClient` (used by `AiChatService` for Gemini). Instead construct an `HttpRequestMessage` per destination with the header attached only to that request.

### Change in `Services/DbInitializer.cs`

Inside the `foreach` loop of `SeedDestinationPhotosAsync`, **replace**:

```csharp
using var response = await httpClient.GetAsync(url);
```

**with**:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, url);
request.Headers.UserAgent.ParseAdd("TouristProjectMVC/1.0 (https://github.com/your-repo; contact@example.com)");
using var response = await httpClient.SendAsync(request);
```

Everything else in the method stays exactly as-is:
- per-destination try/catch
- `Console.Error.WriteLine` warnings
- `originalimage.source` / `thumbnail.source` fallback
- single `await context.SaveChangesAsync()` at the end

## No other files change
- No `Program.cs` HttpClient registration change.
- No title-mapping dictionary change.
- No new named client "Wikipedia" needed; the per-request header is enough and keeps the change fully localized.

## Post-fix validation
1. The previously-failed destinations still have `PhotoUrls = null/empty` (nothing was ever saved due to 403), so the `IsNullOrWhiteSpace` guard naturally re-picks them up.
2. Restart the app in Development.
3. Confirm console shows `200 OK` instead of `403` and that `PhotoUrls` is populated after `SaveChangesAsync`.
