# Plan: Add Conversation History to AI Chat Widget

## Current State
- AI chat widget lives in `Views/Shared/_Layout.cshtml` (markup + inline CSS) and `wwwroot/js/aiChat.js`.
- Backed by `Controllers/AiChatController.cs` → `Services/AiChatService.cs`.
- `aiChat.js` keeps conversation in a plain in-memory `history` array (capped at `MAX_HISTORY = 12`) and resends it as JSON on every request.
- Page reload loses everything. No DB table exists for chat sessions/messages.
- Anonymous visitors and non-Tourist roles already have ephemeral, non-persisted chat.

## Goal
Add a history icon to the widget for signed-in Tourists (`User` role). Clicking it shows past conversations; clicking a conversation loads it into the chat view so the user can continue it. New messages append to the same saved session. Anonymous/other roles keep today's ephemeral behavior and see no history icon.

---

## Step 1 — Data Model

**Create `Models/ChatSession.cs`**
```csharp
public class ChatSession
{
    public int Id { get; set; }
    public int TouristId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MessagesJson { get; set; } = "[]";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime UpdatedDate { get; set; } = DateTime.Now;
}
```

**`Data/TouristContext.cs`**
- Add `public DbSet<ChatSession> ChatSessions { get; set; }` alongside the other `DbSet<>` properties (around line 27, before the constructor).

**Migration**
- Run `dotnet ef migrations add AddChatSessions` from the `Tourist_Project_MVC` project directory.
- Then run `dotnet ef database update`.
- Migration naming follows existing convention: `YYYYMMDDHHMMSS_Description.cs`.

---

## Step 2 — Repository

**Create `Repositories/IChatSessionRepository.cs`**
```csharp
public interface IChatSessionRepository : IRepository<ChatSession>
{
    IEnumerable<ChatSession> GetByTouristId(int touristId);
}
```

**Create `Repositories/ChatSessionRepository.cs`**
```csharp
public class ChatSessionRepository : Repository<ChatSession>, IChatSessionRepository
{
    public ChatSessionRepository(TouristContext context) : base(context) { }

    public IEnumerable<ChatSession> GetByTouristId(int touristId)
    {
        return _context.ChatSessions
            .Where(s => s.TouristId == touristId)
            .OrderByDescending(s => s.UpdatedDate)
            .ToList();
    }
}
```

**`Program.cs`**
- Add `builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();` next to the other repository registrations (around line 62).

---

## Step 3 — View Models

**`View_Model/AiChatVM.cs`**

Add to `AiChatRequestVM`:
```csharp
public int? ChatSessionId { get; set; }
```

Add to `AiChatResponseVM`:
```csharp
public int? ChatSessionId { get; set; }
```

---

## Step 4 — Service Layer

**`Services/AiChatService.cs`**

- Inject `IChatSessionRepository` into the constructor.
- In `GetReplyAsync`, after computing the reply, if `tourist != null`:
  1. **Load or create session**
     - If `request.ChatSessionId` is set:
       - Load `ChatSession` by id.
       - If not found or `session.TouristId != tourist.Id`, **skip persistence entirely** (do not throw, do not modify). Return the normal reply with `ChatSessionId = null`.
     - If `request.ChatSessionId` is null:
       - Create new `ChatSession`:
         - `TouristId = tourist.Id`
         - `Title = DeriveTitle(request.Message)` — first ~40 chars of the user's current message, trimmed, add `"…"` if truncated; fall back to `"New conversation"` if the message is empty (image/audio-only first turn).
         - `MessagesJson = "[]"`
         - `CreatedDate = UpdatedDate = DateTime.Now`
       - `_chatSessionRepo.Add(session); _chatSessionRepo.Save();`
  2. **Append turns**
     - Deserialize `session.MessagesJson` into `List<AiChatMessageVM>` (treat malformed JSON as empty list — do not crash).
     - Append the new user turn: `new AiChatMessageVM { Role = "user", Content = request.Message }`.
     - Append the assistant turn: `new AiChatMessageVM { Role = "assistant", Content = response.Reply }`.
     - If the assistant reply indicates a trip was saved (`response.TripSaved`), the `Reply` text already contains the confirmation — persist it as-is.
     - Reserialize to `session.MessagesJson`.
     - `session.UpdatedDate = DateTime.Now`
     - `_chatSessionRepo.Update(session); _chatSessionRepo.Save();`
  3. **Set response id**
     - `response.ChatSessionId = session.Id;`

- **Important**: Do this AFTER the existing reply logic. If the service returns early (missing API key, Gemini error, blocked prompt, etc.), skip persistence — but for signed-in tourists, you may still want to set `response.ChatSessionId` if a session was already loaded from `request.ChatSessionId`. Actually, per the plan: persistence happens after computing the reply. Early error returns skip it. The client will create a new session on its next successful message.

- **Helper**: `private static string DeriveTitle(string? message)` — returns truncated first message or `"New conversation"`.

- **MAX_HISTORY note**: Keep the existing `TakeLast(16)` Gemini context trimming in `AiChatService` exactly as-is. Persistence is independent; the server loads the full `MessagesJson` and appends, so the transcript grows unbounded on the server side even though the client still caps its in-memory array at 12.

---

## Step 5 — Controller

**`Controllers/AiChatController.cs`**

- Add `IChatSessionRepository` to constructor parameters.
- Extract a private helper for tourist resolution (reuse the exact same cookie/JWT block currently at the top of `Send`):
  ```csharp
  private async Task<Tourist?> ResolveTouristAsync(CancellationToken ct)
  {
      var hasBearerToken = Request.Headers.TryGetValue("Authorization", out var authHeader)
          && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
      var hasIdentityCookie = Request.Cookies.ContainsKey(".AspNetCore.Identity.Application");
      ClaimsPrincipal identity;
      if (hasBearerToken)
      {
          var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
          if (!authResult.Succeeded || authResult.Principal == null) return null;
          identity = authResult.Principal;
      }
      else if (hasIdentityCookie)
      {
          try { await _antiforgery.ValidateRequestAsync(HttpContext); }
          catch (AntiforgeryValidationException) { return null; }
          identity = User;
      }
      else
      {
          identity = User;
      }
      if (identity.Identity?.IsAuthenticated != true || !identity.IsInRole("User")) return null;
      var appUser = await _userManager.GetUserAsync(identity);
      if (appUser == null) return null;
      return _touristRepo.GetOrCreateByApplicationUser(appUser);
  }
  ```
  - Note: `_antiforgery` is already a field. The helper returns `null` on any failure (invalid token, missing cookie, wrong role, etc.).

- **`Send` action**: Replace the inline resolution block with `var tourist = await ResolveTouristAsync(ct);`. Keep the rest of the action identical. The `request.ChatSessionId` field is already part of the model binding from the form.

- **`[HttpGet] GetHistory()`**:
  ```csharp
  [HttpGet]
  public async Task<IActionResult> GetHistory(CancellationToken ct)
  {
      var tourist = await ResolveTouristAsync(ct);
      if (tourist == null)
          return Json(Array.Empty<object>());
      var sessions = _chatSessionRepo.GetByTouristId(tourist.Id)
          .Select(s => new { s.Id, s.Title, s.UpdatedDate })
          .ToList();
      return Json(sessions);
  }
  ```

- **`[HttpGet] GetHistorySession(int id)`**:
  ```csharp
  [HttpGet]
  public async Task<IActionResult> GetHistorySession(int id, CancellationToken ct)
  {
      var tourist = await ResolveTouristAsync(ct);
      if (tourist == null)
          return Json(new { error = "Unauthorized" });
      var session = await _chatSessionRepo.GetByIdAsync(id); // Add this to IRepository if missing, or use Find
      if (session == null || session.TouristId != tourist.Id)
          return NotFound();
      var messages = JsonSerializer.Deserialize<List<AiChatMessageVM>>(session.MessagesJson, 
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
      return Json(new { id = session.Id, title = session.Title, messages });
  }
  ```
  - **Note**: `IRepository<T>` currently does not expose `GetByIdAsync`. Either add `Task<T?> GetByIdAsync(int id)` to `IRepository<T>` and implement it in `Repository<T>`, or use `_context.Set<ChatSession>().FindAsync(id)` directly in the controller. Adding the async method is cleaner and consistent. If you add it, update `Repository<T>`:
    ```csharp
    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);
    ```

---

## Step 6 — Frontend Markup (`Views/Shared/_Layout.cshtml`)

**History toggle button** — inside `.ai-widget-header`, add before the close button, only for signed-in Tourists:
```html
@if (User.Identity.IsAuthenticated && User.IsInRole("User"))
{
    <button type="button" class="ai-widget-history-toggle" id="aiHistoryBtn"
            aria-label="@Localizer["Ai_History"].Value">
        <i class="bi bi-clock-history"></i>
    </button>
}
```

**History panel** — sibling to `.ai-widget-body` and `.ai-widget-footer`, hidden by default:
```html
<div class="ai-widget-history" id="aiWidgetHistory" hidden>
    <div class="ai-widget-history-header">
        <button type="button" class="ai-history-back-btn" id="aiHistoryBackBtn"
                aria-label="@Localizer["Ai_Back"].Value">
            <i class="bi bi-arrow-left"></i>
        </button>
        <span>@Localizer["Ai_History"].Value</span>
    </div>
    <div class="ai-widget-history-list" id="aiHistoryList">
        <!-- populated by aiChat.js -->
    </div>
</div>
```

**Data attributes on `#aiAssistantPanel`** — add alongside existing ones:
```html
data-history-url="@Url.Action("GetHistory", "AiChat")"
data-history-session-url-template="@Url.Action("GetHistorySession", "AiChat", new { id = "__ID__" })"
```

---

## Step 7 — CSS (in the existing `<style>` block in `_Layout.cshtml`)

Add right after the current `.ai-widget-footer` rules (around line 1110), matching the existing visual language:

```css
.ai-widget-history-toggle {
    background: none;
    border: none;
    color: rgba(255,255,255,0.8);
    font-size: 1.2rem;
    cursor: pointer;
    padding: 4px;
    line-height: 1;
    transition: color .2s ease;
}
.ai-widget-history-toggle:hover,
.ai-widget-history-toggle:focus {
    color: #fff;
    outline: none;
}

.ai-widget-history {
    display: none;
    flex-direction: column;
    background: var(--egy-light);
    min-height: 220px;
    max-height: 420px;
    overflow: hidden;
}

.ai-widget-history-mode .ai-widget-body,
.ai-widget-history-mode .ai-widget-footer {
    display: none;
}
.ai-widget-history-mode .ai-widget-history {
    display: flex;
}

.ai-widget-history-header {
    padding: 12px 16px;
    border-bottom: 1px solid rgba(200, 131, 42, 0.2);
    display: flex;
    align-items: center;
    gap: 10px;
    font-weight: 600;
    color: var(--egy-dark);
}

.ai-history-back-btn {
    background: none;
    border: none;
    color: var(--egy-dark);
    font-size: 1.1rem;
    cursor: pointer;
    padding: 2px;
    line-height: 1;
}
.ai-history-back-btn:hover,
.ai-history-back-btn:focus {
    color: var(--egy-primary);
    outline: none;
}

.ai-widget-history-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
}

.ai-history-item {
    padding: 10px 12px;
    border-radius: 8px;
    cursor: pointer;
    transition: background .15s ease;
    border-bottom: 1px solid rgba(0,0,0,0.04);
}
.ai-history-item:last-child {
    border-bottom: none;
}
.ai-history-item:hover,
.ai-history-item:focus {
    background: rgba(200, 131, 42, 0.08);
    outline: none;
}
.ai-history-item-title {
    font-weight: 600;
    font-size: 0.9rem;
    color: var(--egy-dark);
    margin-bottom: 2px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.ai-history-item-date {
    font-size: 0.75rem;
    color: #888;
}

.ai-history-empty {
    text-align: center;
    padding: 32px 16px;
    color: #888;
    font-size: 0.9rem;
}
```

---

## Step 8 — Frontend Behavior (`wwwroot/js/aiChat.js`)

**State variables** — add alongside existing ones:
```js
var currentSessionId = null;
var historyUrl = panel.getAttribute('data-history-url') || '';
var historySessionUrlTemplate = panel.getAttribute('data-history-session-url-template') || '';
```

**History button click** — fetch list, render rows, toggle mode:
```js
var historyBtn = document.getElementById('aiHistoryBtn');
if (historyBtn) {
    historyBtn.addEventListener('click', async function () {
        if (!historyUrl) return;
        var listEl = document.getElementById('aiHistoryList');
        listEl.innerHTML = '<div class="ai-history-empty">Loading…</div>';
        panel.classList.add('ai-widget-history-mode');

        try {
            var response = await fetch(historyUrl, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            var sessions = await response.json();
            listEl.innerHTML = '';
            if (!sessions || sessions.length === 0) {
                listEl.innerHTML = '<div class="ai-history-empty">@Localizer["Ai_HistoryEmpty"].Value</div>';
                return;
            }
            sessions.forEach(function (s) {
                var item = document.createElement('div');
                item.className = 'ai-history-item';
                item.setAttribute('tabindex', '0');
                item.setAttribute('role', 'button');
                var dateStr = new Date(s.updatedDate).toLocaleDateString();
                item.innerHTML = '<div class="ai-history-item-title">' + escapeHtml(s.title) + '</div>' +
                                 '<div class="ai-history-item-date">' + escapeHtml(dateStr) + '</div>';
                item.addEventListener('click', function () { loadHistorySession(s.id); });
                item.addEventListener('keydown', function (e) { if (e.key === 'Enter') loadHistorySession(s.id); });
                listEl.appendChild(item);
            });
        } catch (err) {
            listEl.innerHTML = '<div class="ai-history-empty">@Localizer["Ai_Error"].Value</div>';
        }
    });
}
```

**Back button click** — just toggle mode off:
```js
var historyBackBtn = document.getElementById('aiHistoryBackBtn');
if (historyBackBtn) {
    historyBackBtn.addEventListener('click', function () {
        panel.classList.remove('ai-widget-history-mode');
    });
}
```

**Load a history session** — fetch messages, re-render, switch back to chat:
```js
async function loadHistorySession(id) {
    var url = historySessionUrlTemplate.replace('__ID__', id);
    try {
        var response = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.ok) return;
        var data = await response.json();
        messagesEl.innerHTML = '';
        history = [];
        currentSessionId = data.id;
        if (data.messages && Array.isArray(data.messages)) {
            data.messages.forEach(function (m) {
                appendMessage(m.role, m.content);
                history.push({ role: m.role, content: m.content });
            });
        }
        panel.classList.remove('ai-widget-history-mode');
        if (input) input.focus();
    } catch (err) {
        // silently keep current view on error
    }
}
```

**`sendMessage()` modifications**:
1. Append `ChatSessionId` to `formData`:
   ```js
   formData.append('ChatSessionId', currentSessionId || '');
   ```
2. After successful response, store `data.chatSessionId`:
   ```js
   if (data && data.chatSessionId) {
       currentSessionId = data.chatSessionId;
   }
   ```

**Helper — `escapeHtml`** — add near the top of the IIFE (before event handlers) to prevent XSS in history titles:
```js
function escapeHtml(text) {
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
```

---

## Step 9 — Localization

**`Resources/SharedResource.en.resx`** — add after the existing `Ai_ViewTrip` key:
```xml
<data name="Ai_History" xml:space="preserve">
  <value>History</value>
</data>
<data name="Ai_Back" xml:space="preserve">
  <value>Back</value>
</data>
<data name="Ai_HistoryEmpty" xml:space="preserve">
  <value>You have no saved conversations yet.</value>
</data>
```

**`Resources/SharedResource.ar.resx`** — add after the existing `Ai_ViewTrip` key:
```xml
<data name="Ai_History" xml:space="preserve">
  <value>السجل</value>
</data>
<data name="Ai_Back" xml:space="preserve">
  <value>رجوع</value>
</data>
<data name="Ai_HistoryEmpty" xml:space="preserve">
  <value>لا توجد محادثات محفوظة بعد.</value>
</data>
```

---

## Step 10 — Build & Verification

1. `dotnet build` — fix any compile errors (especially from new interface methods / DI registrations).
2. `dotnet ef migrations add AddChatSessions` then `dotnet ef database update`.
3. Run the app, sign in as a Tourist (`User` role).
4. Open AI widget, send a message. Confirm it succeeds.
5. Click history icon — confirm the conversation appears with a reasonable title.
6. Reload the page, open history, click the conversation — confirm transcript loads and you can continue typing. Check DB that `UpdatedDate` and `MessagesJson` grew (same row, not a new row).
7. Start a second new conversation without touching history — confirm a second session is created.
8. Sign out / browse anonymously — confirm history icon is hidden and chat still works.
9. Try accessing `GetHistorySession` with another user's session id (e.g., via browser dev tools or direct URL) — confirm 404.

---

## Out of Scope
- Deleting/renaming chat sessions.
- Pagination for users with very long histories.
- Push notifications for chat.
- Changing the Gemini reply-generation logic.

## Risks
- If `MessagesJson` becomes very large, session loads/saves could slow down. Mitigation: this is a chat widget, not a high-throughput system; the JSON stays small for typical usage.
- A tampered `ChatSessionId` in the request body is handled server-side by the ownership check.
- Anonymous users and non-Tourist roles never see the history UI, so no behavior change for them.
