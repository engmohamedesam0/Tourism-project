# Nav Reorganization Plan — _Layout.cshtml

## Scope
`Tourist_Project_MVC/Views/Shared/_Layout.cshtml`

Exact changes only in:
1. **User-icon dropdown** (`#acctDropdown`, lines ~300-329)
2. **Tourist authenticated nav block** (`<!-- ============ TOURIST EXPERIENCE ============ -->`, lines ~471-514)

Leave Admin, Sponsor, guest nav blocks and footer sitemap untouched.

---

## Change A: Profile & Support into User-Icon Dropdown

### Remove from tourist top-level nav
Delete these two `<li class="nav-item">` blocks from the tourist experience (lines 499-508):
- `<li>` with `asp-controller="TouristProfile"` / `bi-person-fill` / `Nav_Profile`
- `<li>` with `asp-controller="TouristSupport"` / `bi-headset` / `Nav_Support`

### Add to `#acctDropdown`
Inside `<ul class="dropdown-menu ..." aria-labelledby="acctDropdown">` (line 308), insert before the existing `<li><hr class="dropdown-divider" /></li>` (line 321), guarded by a role check so only tourists see them:

```csharp
@if (!User.IsInRole("Admin") && !User.IsRole("Sponsor"))
{
    <li>
        <a class="dropdown-item" asp-controller="TouristProfile" asp-action="Index">
            <i class="bi bi-person-fill me-2"></i> @Localizer["Nav_Profile"].Value
        </a>
    </li>
    <li>
        <a class="dropdown-item" asp-controller="TouristSupport" asp-action="Index">
            <i class="bi bi-headset me-2"></i> @Localizer["Nav_Support"].Value
        </a>
    </li>
}
```

**Role-guard rationale:** Matches the existing `else if (User.Identity.IsAuthenticated)` tourist branch in the main `<ul>` nav. Admins still see their Add Role / Assign Roles + divider + Logout. Sponsors still see only the divider + Logout. Tourists see Profile → Support → divider → Logout.

Keep existing Admin-only block (lines 309-322) and existing Logout block (lines 323-327) unchanged.

---

## Change B: Reorder Features After Rewards in Tourist Nav

In the tourist experience block, reorder the three items so the final sequence is:

1. Explore
2. Trip
3. Near Me
4. Rewards
5. Features
6. About

Move the existing Features `<li class="nav-item">` block (lines 489-493) to immediately after the Rewards block (lines 494-498), using the exact same markup, icon (`bi-phone-fill`), and localizer key (`Nav_Features`).

No markup/icon/key changes — pure copy-paste reordering.

---

## Validation
1. Run the app and log in as a Tourist: verify navbar shows `Explore → Trip → Near Me → Rewards → Features → About` (no Profile/Support top-level).
2. Click the account dropdown (user-icon): verify it shows `My Profile → Support → ─── Logout`.
3. Log in as Admin: verify navbar unchanged, dropdown shows `Add Role → Assign Roles → ─── Logout` (no Profile/Support).
4. Log in as Sponsor: verify navbar unchanged, dropdown shows only `─── Logout` (no Profile/Support).
5. Verify footer sitemap still contains Profile, Rewards, Support, Features links unchanged.
6. Confirm no build errors.
