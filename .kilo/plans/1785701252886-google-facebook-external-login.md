# Google & Facebook External Login Implementation

## Context

Add Google and Facebook OAuth external login to the existing ASP.NET MVC project, remove the Apple button from the social login row, and wire up the new authentication providers end-to-end.

## Files to Modify (7 files)

### 1. `Tourist_Project_MVC.csproj` — Add NuGet packages

Add two new `PackageReference` entries (version `10.0.10` matching the existing JwtBearer version):

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.10" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.Facebook" Version="10.0.10" />
```

Insert them before the existing `Microsoft.AspNetCore.Authentication.JwtBearer` reference.

After editing, run `dotnet restore`.

### 2. `appsettings.json` — Add Authentication section

Add a new top-level `"Authentication"` section after the `"ArcGIS"` block:

```json
"Authentication": {
  "Google": {
    "ClientId": "",
    "ClientSecret": ""
  },
  "Facebook": {
    "AppId": "",
    "AppSecret": ""
  }
},
```

Values remain empty here — real secrets go in User Secrets only.

### 3. `Program.cs` — Insert AddGoogle and AddFacebook before AddJwtBearer

Find the existing chain starting at line 97:

```csharp
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
```

Replace it so that `.AddGoogle(...)` and `.AddFacebook(...)` are inserted before `.AddJwtBearer(...)`:

```csharp
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/signin-google";
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? "";
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? "";
        options.CallbackPath = "/signin-facebook";
    })
    .AddJwtBearer(options =>
    {
        // ...existing JWT config, unchanged...
    });
```

Do not touch anything else in the JWT block.

### 4. `Controllers/AccountController.cs` — Refactor + Add external login actions

**4a. Add using directive**

Add `using Microsoft.AspNetCore.Authentication;` at the top (needed for `FindFirstValue`).

**4b. Refactor existing Login POST action**

Extract the inline sponsor-approval gate and post-sign-in logic from the existing `Login(LoginViewModel loginUser)` POST action into two private helper methods:

```csharp
private async Task<IActionResult?> CheckSponsorApprovalGateAsync(ApplicationUser user)
{
    var approval = await _context.SponsorApprovalRequests
        .FirstOrDefaultAsync(r => r.ApplicationUserId == user.Id);
    if (approval != null && approval.Status == "Pending")
        return RedirectToAction("SponsorApprovalStatus", new { status = "pending" });
    if (approval != null && approval.Status == "Rejected")
        return RedirectToAction("SponsorApprovalStatus", new { status = "rejected" });
    return null;
}

private async Task<IActionResult> CompletePostLoginAsync(ApplicationUser user)
{
    var loginTourist = _touristRepo.GetOrCreateByApplicationUser(user);
    var today = DateTime.Today;
    var progress = await _gamificationService.GetOrInitializeProgressAsync(loginTourist.Id);

    bool isFirstLogin = progress.LastLoginDate == null;
    TempData["ShowWelcome"] = true;
    TempData["IsFirstLogin"] = isFirstLogin;

    if (progress.LastLoginDate == null || progress.LastLoginDate.Value < today.AddDays(-1))
    {
        progress.LoginStreak = progress.LastLoginDate.HasValue && progress.LastLoginDate.Value == today.AddDays(-1)
            ? progress.LoginStreak + 1
            : 1;
        progress.LastLoginDate = today;
        _context.UserProgress.Update(progress);
        await _context.SaveChangesAsync();
        await _gamificationService.AwardXPAsync(loginTourist.Id, 10, "daily-login");
    }

    if (await userManager.IsInRoleAsync(user, "Admin"))
        return RedirectToAction("Index", "Tourist");
    if (await userManager.IsInRoleAsync(user, "Sponsor"))
        return RedirectToAction("Index", "SponsorPortal");
    return RedirectToAction("Index", "Explore");
}
```

Then update the existing `Login` POST action body to call these two helpers instead of the inline code.

**4c. Add `ExternalLogin` POST action**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult ExternalLogin(string provider)
{
    var redirectUrl = Url.Action("ExternalLoginCallback", "Account");
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Challenge(properties, provider);
}
```

**4d. Add `ExternalLoginCallback` GET action**

```csharp
[HttpGet]
public async Task<IActionResult> ExternalLoginCallback(string? remoteError = null)
{
    if (remoteError != null)
    {
        ModelState.AddModelError("", $"External login error: {remoteError}");
        return View("Login");
    }

    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        ModelState.AddModelError("", "Could not load external login information.");
        return View("Login");
    }

    var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
    if (signInResult.Succeeded)
    {
        var linkedUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (linkedUser == null) return RedirectToAction("Login");

        var gate = await CheckSponsorApprovalGateAsync(linkedUser);
        if (gate != null) return gate;

        return await CompletePostLoginAsync(linkedUser);
    }

    var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
    if (string.IsNullOrWhiteSpace(email))
    {
        ModelState.AddModelError("", $"{info.LoginProvider} did not share an email address, so we can't sign you in.");
        return View("Login");
    }

    var existingUser = await userManager.FindByEmailAsync(email);
    if (existingUser == null)
    {
        var firstName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.GivenName) ?? "";
        var lastName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Surname) ?? "";

        existingUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(existingUser);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError("", error.Description);
            return View("Login");
        }
        await userManager.AddToRoleAsync(existingUser, "Tourist");
    }

    var addLoginResult = await userManager.AddLoginAsync(existingUser, info);
    if (!addLoginResult.Succeeded)
    {
        foreach (var error in addLoginResult.Errors)
            ModelState.AddModelError("", error.Description);
        return View("Login");
    }

    await signInManager.SignInAsync(existingUser, isPersistent: false);

    var gate2 = await CheckSponsorApprovalGateAsync(existingUser);
    if (gate2 != null) return gate2;

    return await CompletePostLoginAsync(existingUser);
}
```

### 5. `Views/Account/Login.cshtml` — Replace social-login-row block

Replace the `<div class="social-login-row">...</div>` block (lines 169-196) with the new version containing only Google and Facebook as real submit forms, Apple removed entirely:

```html
<div class="social-login-row">
    <form asp-action="ExternalLogin" asp-controller="Account" method="post">
        <input type="hidden" name="provider" value="Google" />
        <button type="submit" class="social-icon-btn" aria-label="Continue with Google">
            <svg viewBox="0 0 48 48">
                <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.6 29.3 35.5 24 35.5 16.5 35.5 10.5 29.5 10.5 22S16.5 8.5 24 8.5c3.7 0 7 1.4 9.5 3.6l5.7-5.7C35.6 3 30.1 1 24 1 11.8 1 2 10.8 2 23s9.8 22 22 22c11 0 21-8 21-22 0-1.5-.1-2.5-.4-3.5z"/>
                <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.6 15.9 18.9 13 24 13c3.1 0 6 1.1 8.2 3l6.2-6.2C34.9 6.5 29.7 4.5 24 4.5c-7.8 0-14.5 4.4-17.7 10.2z"/>
                <path fill="#4CAF50" d="M24 44c5.5 0 10.5-1.9 14.3-5.2l-6.6-5.4c-2 1.5-4.7 2.6-7.7 2.6-5.3 0-9.7-3.6-11.3-8.4l-6.5 5C9.4 39.4 16.1 44 24 44z"/>
                <path fill="#1976D2" d="M43.6 20.5H24v8h11.3c-.8 2.2-2.2 4.1-4.1 5.4l6.6 5.4C41.6 36 44 30 44 23c0-1.5-.1-2.5-.4-2.5z"/>
            </svg>
        </button>
    </form>

    <form asp-action="ExternalLogin" asp-controller="Account" method="post">
        <input type="hidden" name="provider" value="Facebook" />
        <button type="submit" class="social-icon-btn" aria-label="Continue with Facebook">
            <svg viewBox="0 0 24 24">
                <path fill="#1877F2" d="M24 12.07C24 5.4 18.63 0 12 0S0 5.4 0 12.07C0 18.1 4.39 23.1 10.13 24v-8.44H7.08v-3.49h3.05V9.41c0-3.02 1.79-4.7 4.53-4.7 1.31 0 2.68.24 2.68.24v2.97h-1.51c-1.49 0-1.96.93-1.96 1.89v2.26h3.33l-.53 3.49h-2.8V24C19.61 23.1 24 18.1 24 12.07z"/>
            </svg>
        </button>
    </form>
</div>
```

### 6. `Views/Account/Register.cshtml` — Replace social-login-row block

Same replacement as Login.cshtml — replace the `<div class="social-login-row">...</div>` block (lines 329-356) with the identical Google/Facebook-only version above. Remove the Apple button entirely.

### 7. `wwwroot/css/login.css` — Two CSS changes

**7a.** Right after the `.social-login-row { ... }` rule (line 469), add:

```css
.social-login-row form {
    display: contents;
}
```

This ensures the new `<form>` wrappers don't break the existing flex layout of the buttons.

**7b.** Delete the `.social-icon-btn:last-child svg path` rule entirely (lines 497-499):

```css
.social-icon-btn:last-child svg path {
    fill: #111 !important;
}
```

This rule forced the last icon (Apple) to black and would now wrongly recolor the Facebook icon.

## Post-Change Steps (not part of code edits)

1. Run `dotnet restore` to restore the new NuGet packages.
2. Create a Google OAuth Client ID (console.cloud.google.com → APIs & Services → Credentials → OAuth client ID → Web application) and a Facebook App (developers.facebook.com → add "Facebook Login" product).
3. Set redirect/callback URIs to `https://<domain>/signin-google` and `https://<domain>/signin-facebook` (and localhost equivalents for dev).
4. Store real values with `dotnet user-secrets set`:
   ```
   dotnet user-secrets set "Authentication:Google:ClientId" "..."
   dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
   dotnet user-secrets set "Authentication:Facebook:AppId" "..."
   dotnet user-secrets set "Authentication:Facebook:AppSecret" "..."
   ```
5. Run the project and test both "Continue with Google" and "Continue with Facebook" on `/Account/Login` and `/Account/Register`.

## Verification

- Both external login buttons appear on Login and Register pages (Apple removed).
- Clicking Google/Facebook redirects to the provider's OAuth consent screen.
- After auth, the callback creates a new Tourist-role user if no local account exists, or links the external login to an existing account.
- Sponsor approval gate and post-login streak/XP/role-redirect logic works identically for external and password login.
- `dotnet build` succeeds with no errors.
