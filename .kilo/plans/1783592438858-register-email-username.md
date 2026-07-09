# Fix: "Username 'mohamed Hamed' is invalid" on Register → use Email as UserName (Option A)

## Context
- `AccountController.Register` currently sets `UserName = $"{FirstName} {LastName}"` (AccountController.cs:70). Names with a space violate ASP.NET Core Identity's default `AllowedUserNameCharacters` (letters, digits, `-._@+`), producing: `Username 'mohamed Hamed' is invalid, can only contain letters or digits`.
- There is **no JS auto-fill** in `Register.cshtml` — the value comes only from the controller. (Confirmed via grep.)
- Prior task explicitly required **removing the Username field entirely** and replacing it with First/Last Name, so Option B (re-introducing a Username input) is out of scope/contradicts that directive.
- `Program.cs` configures `AddIdentity` with only `Password.RequiredLength` and `User.RequireUniqueEmail = true`; `AllowedUserNameCharacters` keeps Identity defaults.
- `UserName` is also used as a **display handle** in: `RoleController` admin table (`AccountRow.UserName`), `AdminSupportController` responder name, and `TouristRepository.GetOrCreateByApplicationUser` fallback for `Tourist.Name` (TouristRepository.cs:58). `Login` resolves via `FindByNameAsync(loginUser.UserName)`.

## Decision
Implement **Option A**: drop username derivation; set `ApplicationUser.UserName = Email` at registration. Email always satisfies Identity's allowed characters and is already unique (`RequireUniqueEmail = true`). This is consistent with the prior "remove Username field" requirement and removes the bug class.

## Changes
1. **AccountController.cs (Register POST)** — replace the derivation:
   - Remove: `var userName = $"{userFromRequest.FirstName} {userFromRequest.LastName}".Trim();` and `UserName = userName,`
   - Set: `UserName = userFromRequest.UserEmail,`
   - Keep **everything else unchanged**: profile-picture upload, AccountType Sponsor gate (`SponsorApprovalRequest`), `userManager.AddToRoleAsync(createdUser, "User")`, and `tourist.Name = $"{createdUser.FirstName} {createdUser.LastName}".Trim();` (so `Tourist.Name` remains the real name, not the email).
2. **RegisterViewModel.cs** — no change needed (already has no `UserName`; `UserEmail` is `[Required][EmailAddress]`).
3. **Register.cshtml** — no change needed (Username field already removed; no JS auto-fill).
4. **Login.cshtml (coherence, not registration flow)** — relabel the login field from "User Name" to "Email or Username" so new users know to enter their email. Leave `AccountController.Login` logic as-is: `FindByNameAsync(loginUser.UserName)` resolves by username = email for new users and by the old username for pre-existing accounts. No controller change required for correctness.

## Why existing accounts still log in
- Pre-fix accounts store their original chosen username. `FindByNameAsync` still finds them by that username. No data migration needed.
- New accounts store email as username; `FindByNameAsync(email)` finds them. Login works for both.

## Side-effect note (acceptable, no action required)
- Admin `RoleController` table and `AdminSupport` responder name will show the email for new users (the `RoleController` view already has a separate Email column). `Tourist.Name` is explicitly set from First/Last at registration, so the `TouristRepository` `UserName` fallback only affects unlinked/edge users.

## Validation / Verification
- `dotnet build` → expect **0 errors** (only pre-existing nullability warnings).
- Register a fresh user with `FirstName=mohamed`, `LastName=Hamed`, a valid email, password ≥12 chars → completes **without** the "invalid username" error; redirect to Login; role `User` assigned.
- Login with that email + password → lands on `Explore`; `/TouristReward`, `/TouristProfile`, `/TouristSupport` open (role `User`).
- Re-login with a pre-existing account (old username) → still works.
- Sponsor sign-up path unchanged (AccountType=Sponsor still gates via approval; username=email).
