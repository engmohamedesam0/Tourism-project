# EGYXPLORE — Role-Aware AI Agent: Tool Mapping

This document maps every AI-callable tool to the **existing** application
functionality it reuses. The agent never duplicates CRUD logic and never touches
the database directly — every tool goes through the application's own
repositories/services, exactly like the existing controllers.

## Architecture

```
User
 → AI Chat widget (wwwroot/js/aiChat.js → /AiChat/Send)
 → AiAgentOrchestrator (Gemini function-calling loop)
 → AiToolRegistry (role filter + confirmation gate)
 → Role tool sets (GuestAiTools / TouristAiTools / SponsorAiTools / AdminAiTools)
 → Existing repositories / services / controllers logic
 → Server-side authorization (Identity roles + ownership checks)
 → DbContext (PostgreSQL)
 → Result → AI response
```

Key classes (all under `Services/` unless noted):

| Class | Responsibility |
|---|---|
| `Services/AiAgent/AiIdentityResolver.cs` | Resolves current user/role/tourist/sponsor **server-side** from the ASP.NET Core Identity cookie or mobile JWT. Never trusts client/LLM input. |
| `Services/AiAgent/AiToolRegistry.cs` | Role-filtered tool lookup + execution; re-checks role at every call; intercepts state-changing tools into pending confirmations. |
| `Services/AiAgent/AiPendingActionStore.cs` | In-memory store of confirmations awaiting user approval (10-min expiry, bound to the user). |
| `Services/AiAgent/AiAgentOrchestrator.cs` | Gemini `generateContent` loop, meta tools `confirm_pending_action` / `cancel_pending_action`, system prompt per role. |
| `Services/AiAgent/AiStarterQuestionsService.cs` | Role-based starter questions (role derived server-side). |
| `Services/ChatHistoryService.cs` | ChatSession persistence (tourists only, as before). |
| `Controllers/AiChatController.cs` | `Send`, `StarterQuestions`, `ConfirmPendingAction`, `CancelPendingAction`, history endpoints. |

Roles in Identity: **User** (tourist), **Sponsor**, **Admin**; anonymous = **Guest**.

## Guest tools (public, read-only)

| AI Tool | Reuses |
|---|---|
| `search_destinations` | `IDestinationRepository.GetAll()` (Active only) — same data as `ExploreController` / `DestinationController` |
| `get_destination_details` | `IDestinationRepository.GetById` — same data as `DestinationController.Details` |
| `get_public_rewards` | `IRewardRepository.GetAll()` (Active) — same data as public `RewardController.Index` |
| `get_site_overview` | Static EGYXPLORE feature summary + live destination count from `IDestinationRepository` |
| `get_recommendations` | `IDestinationRepository.GetAll()` ordered by `Rating` (matches Explore sorting) |

Guests can **never** create/update/delete — the registry has no write tools for
the Guest role, and every tool re-checks the role.

## Tourist tools (Identity role `User`)

| AI Tool | Reuses | Confirmation |
|---|---|---|
| `search_destinations`, `get_destination_details` | shared Guest tools (all roles) | — |
| `create_trip` | `TripController.Create` / previous `AiChatService.save_trip_plan` (via `ITripPlanRepository`), owner = `identity.Tourist.Id` | ✅ yes |
| `get_my_trips` | `TripController.Index` logic (`ITripPlanRepository.GetAllWithDetails` filtered by tourist) | — |
| `update_trip` | `TripController.Create`/builder logic (title, dates, budget, companions, stops) | ✅ yes |
| `delete_trip` | `IRepository<TripPlan>.Delete` + `ITripPlanRepository.RemoveTripDestinations` (owner check) | ✅ yes |
| `add_destination_to_trip` | previous `AiChatService.add_destination_to_trip` (`ITripPlanRepository.AddStop`) | — (small reversible edit, same as before) |
| `remove_destination_from_trip` | previous `AiChatService.remove_destination_from_trip` (`RemoveStop` + renumber) | — |
| `reorder_trip_destinations` | previous `AiChatService.reorder_trip_destinations` (`UpdateStop`) | — |
| `get_destination_photos` | previous `AiChatService.get_destination_photos` | — |
| `get_my_profile` | `TouristProfileController.Index` data (points/level via `IGamificationService`, badges) | — |
| `update_my_profile` | `TouristProfileController.Edit` (`UserManager.UpdateAsync` + tourist preferences) | ✅ yes |
| `get_recommended_destinations` | same rating-based query as `get_recommendations` | — |

Ownership: trip/profile lookups are always filtered by the server-resolved
`identity.Tourist.Id`. A Tourist ID or trip ID typed by the user is never used.

## Sponsor tools (Identity role `Sponsor`)

| AI Tool | Reuses | Confirmation |
|---|---|---|
| `create_branch` | `SponsorBranchController.Create` (`IBranchRepository.Add` + `IArcGISSyncService.SyncBranchesAsync`); `SponsorId` always from `identity.Sponsor.Id` | ✅ yes |
| `get_my_branches` | `SponsorBranchController.Index` (`IBranchRepository.GetBySponsorId`) | — |
| `update_branch` | `SponsorBranchController.Edit` (owner check `branch.SponsorId == sponsor.Id`) | ✅ yes |
| `delete_branch` | `SponsorBranchController.DeleteConfirmed` | ✅ yes |
| `get_my_rewards` | `SponsorRewardController.Index` (`IRewardRepository.GetBySponsorId`) | — |
| `create_reward` | `SponsorRewardController.Create` (+ `RewardBranch` sync) | ✅ yes |
| `update_reward` | `SponsorRewardController.Edit` (owner check) | ✅ yes |
| `delete_reward` | `SponsorRewardController.DeleteConfirmed` (soft: `Status = "Removed"`) | ✅ yes |
| `get_my_profile` | Sponsor record from `ISponsorRepository.GetOrCreateByApplicationUser` + branch/reward counts | — |

Notes:
- **Branches have no price field** in this project (`Models/Branch.cs`). The tool
  therefore asks for name/address/location (Egyptian city or lat/long) and
  explains prices are managed through rewards. No database fields were invented.
- Rewards have **only `ExpirationDate`** (no start-date field) — the AI treats
  "ends on X" as the expiration date.

## Admin tools (Identity role `Admin`)

| AI Tool | Reuses | Confirmation |
|---|---|---|
| `get_platform_stats` | `AdminDashboardController` overview aggregates (`TouristContext`) | — |
| `get_users_list` | `RoleController.ManageAccounts` data (`UserManager.Users` + `GetRolesAsync`) | — |
| `get_sponsors_list` | `ISponsorRepository.GetAll` | — |
| `change_user_role` | `RoleController.AssignRole`/`ManageAccounts` (`UserManager.RemoveFromRolesAsync` + `AddToRoleAsync`); own role cannot be changed | ✅ yes |
| `create_reward` | `RewardController.Create` (admin picks sponsor from `ISponsorRepository`) | ✅ yes |
| `update_reward` | `RewardController.Edit` | ✅ yes |
| `delete_reward` | `RewardController.DeleteConfirmed` | ✅ yes |
| `create_destination` | `AdminDashboardController.AddDestination` (ArcGIS-first flow via `IArcGISSyncService.AddDestinationToArcGISAsync` + `SyncDestinationsFromArcGIS`) | ✅ yes |
| `update_destination` | `DestinationController.Edit` (ArcGIS-first via `UpdateDestinationOnArcGISAsync`, then local row) | ✅ yes |
| `delete_destination` | `DestinationController.DeleteConfirmed` (ArcGIS delete, then transactional cleanup of missions/trip stops/reviews/favorites) | ✅ yes |

## Confirmation & security

- **Every state-changing tool** (`RequiresConfirmation = true`) is *pre-validated*
  and parked as a pending action — nothing is written until the user confirms via
  the chat buttons or in conversation (`confirm_pending_action` / `cancel_pending_action`).
- **Confirmation executes with a fresh identity check** — role, ownership and
  business rules are re-evaluated at execution time, so a stale token, a
  different user, or a changed role fails closed.
- **No IDOR / impersonation**: entity IDs are never taken from user text; they
  come from the server-injected context (destination catalog, the user's own
  trips/branches/rewards) or from tool results.
- **No role escalation**: even if the model is tricked ("make me Admin"), the
  tool registry refuses any tool the current Identity role cannot call, and
  `change_user_role` cannot affect the caller's own account.
- **No direct DB access**: the orchestrator only calls `AiToolRegistry`, which
  only calls the existing repositories/services.
- **No sensitive leakage**: exceptions are logged server-side; the user only ever
  sees friendly messages.

## Chat widget (frontend)

- `Views/Shared/_Layout.cshtml` — widget markup + `data-starter-url`,
  `data-confirm-url`, `data-cancel-url`.
- `wwwroot/js/aiChat.js` — fetches starter questions (server-provided role),
  renders 3 clickable chips per role, renders Confirm/Cancel buttons when a
  pending action token is returned, loading/disabled states, free-form chat
  unchanged.
- `wwwroot/css/aiChat.css` — chips + confirmation button styles (light/dark).
