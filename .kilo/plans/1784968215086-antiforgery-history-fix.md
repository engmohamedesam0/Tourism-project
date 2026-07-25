# Fix: Chat history always shows empty despite data in DB

## Root cause (confirmed)

`ResolveTouristAsync()` in `AiChatController.cs` runs antiforgery validation for cookie-authenticated requests. `Send` includes the `RequestVerificationToken` header, but `GetHistory` and `GetHistorySession` fetches in `aiChat.js` do not, so `ValidateRequestAsync` always throws, `ResolveTouristAsync` returns `null`, and history always appears empty.

## Changes

### 1. `Tourist_Project_MVC/wwwroot/js/aiChat.js` — Add antiforgery token to history fetches

- In `loadHistorySession(id)` (line 112): add `'RequestVerificationToken': getAntiforgeryToken()` to the `headers` object alongside the existing `'X-Requested-With': 'XMLHttpRequest'`.
- In the `#aiHistoryBtn` click handler (line 145): add `'RequestVerificationToken': getAntiforgeryToken()` to the `headers` object alongside the existing `'X-Requested-With': 'XMLHttpRequest'`.

`getAntiforgeryToken()` already exists in the file (line 61) — reuse it.

### 2. `Tourist_Project_MVC/Controllers/AiChatController.cs` — Remove diagnostic logging

- Remove the `_logger.LogInformation(...)` block at lines 112-117 (after `var tourist = await ResolveTouristAsync(ct);`). This was temporary diagnostic logging, not a permanent improvement.
- Keep `ILogger<AiChatController>` injection and the try/catch + `_logger.LogError(...)` in `AiChatService.cs` around `PersistChatAsync` — those are genuine.

## Verification

1. Hard-refresh the page (Ctrl+Shift+R).
2. Click the history icon — confirm 7 existing conversations appear.
3. Click one — confirm messages load and the conversation can continue.
4. Send a new message, refresh, open history — confirm the new conversation appears.