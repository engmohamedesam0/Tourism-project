# EGYXPLORE — Brief #1 Follow-Up: Duplicate-Key Fix + Back-Office Localization

> Implementation-ready plan. **Item 1 is already implemented** (source edits applied + verified). Item 2 is the remaining work; execute it in contained batches as described to avoid agent output-limit failures seen in a prior attempt.

## Context
- Solution root: `Tourist_Project_MVC-main/Tourist_Project_MVC/` (code lives under the `Tourist_Project_MVC-main` subfolder, NOT the repo root).
- Localization: ASP.NET Core `IStringLocalizer<SharedResource>` injected via `Views/_ViewImports.cshtml` as `@Localizer`, consumed as `@Localizer["Key"].Value`.
- Two shared resx files hold every string: `Resources/SharedResource.en.resx` (default/fallback) and `Resources/SharedResource.ar.resx`. Both MUST stay in sync (every `en` key needs an `ar` counterpart).
- Already localized (leave alone): tourist pages, `_Layout.cshtml`, `Account/Login`, `Account/Register`, `Trip/Index`, `NearMe/*`, `TouristProfile/*`, `TouristReward/*`.
- **Folder-name correction:** The brief says the sponsor folder is `Sponser`. On disk it is `Sponsor` (correct spelling) — there is **no** `Sponser` folder. Target is `Views/Sponsor/*`.

---

## Item 1 — Duplicate resource-key fix — ✅ DONE (verified)
Root cause: `Feature1/2/3_Title/Text` existed twice (Home block + Features block) → 12× `MSB3568`.
Applied changes (both resx + view):
- Renamed the **Features-page** block's 7 entries to `Features_Feature1_Title` … `Features_Feature7_Text` (values preserved). Home block `Feature1/2/3_Title/Text/Link` left untouched.
- `Views/Features/Index.cshtml` updated: all 14 `FeatureN_*` refs → `Features_FeatureN_*` (lines 40–162).
- Confirmed `ar.resx` had a leftover duplicate partial block removed; both resx now clean (7 `Features_Feature*` keys each, no `Feature1_Title` duplicates).
Expected result after build: 12 `MSB3568` warnings gone; pre-existing `CS8600/CS8602/CS8618` nullability warnings are unrelated and OK.

---

## Item 2 — Localize remaining back-office views (Phase C)

### Convention
- Wrap every **static, user-visible UI string** with `@Localizer["Key"].Value` (same pattern as tourist pages).
- **Key naming:** `Prefix_ElementName` per folder. Use the prefix table below. All prefixes are unique → no cross-folder collisions.
- **Do NOT localize:** model/data-bound values (`@item.Name`, `@item.Description`, `@item.City`, numbers, dates, IDs, `@ViewBag.*` data, DB enum **values** used in comparisons); `asp-controller/action/route-*` and URL `href/src`; JS string literals inside `<script>`.
- **DO localize:** headings, subheadings, `ViewData["Title"]`, section titles, labels, button/link text, table headers, `placeholder=`, static help/intro text, empty-state messages, status **display labels** (wrap only the visible label; keep the `switch`/`if` comparison on the RAW DB string).
- For each new key add a `<data name="Key" xml:space="preserve"><value>...</value></data>` entry to **both** resx (en = English, ar = Arabic). Append before `</root>`. en and ar key sets must be identical.

### Prefix table (folder → prefix) — use EXACTLY these
| Folder | Prefix | Why not a shorter one |
|---|---|---|
| Views/Sponsor/* | `Sponsor_` | — |
| Views/SponsorBranch/* | `SponsorBranch_` | — |
| Views/SponsorRedemption/Index | `SponsorRedemption_` | — |
| Views/SponsorReview/Index | `SponsorReview_` | — |
| Views/SponsorApproval/Index | `SponsorApproval_` | — |
| Views/SponsorPortal/* | `SponsorPortal_` | — |
| Views/SponsorReward/* | `SponsorReward_` | — |
| Views/SponsorNotification/* (+ partials) | `SponsorNotification_` | — |
| Views/Destination/* | `Destination_` | — |
| Views/TripPlan/* | `TripPlan_` | `Trip_*` reserved (Trip filter page) |
| Views/Mission/* | `Mission_` | — |
| Views/Reward/* | `RewardAdmin_` | `Reward_*` reserved (tourist rewards) |
| Views/Tourist/* | `TouristAdmin_` | `TouristProfile_*`/avoid clash |
| Views/Role/* | `RoleAdmin_` | `Role_Admin` etc. already exist |
| Views/AdminSupport/* | `AdminSupport_` | — |
| Views/TouristSupport/* | `TouristSupport_` | — |
| Views/Account/Reset | `Account_` | `Account_ResetTitle` etc. |
| Views/Trip/Details | `TripDetails_` | `Trip_*` reserved |
| Views/Shared/_ReviewsCarousel | `Reviews_` | shared partial, also on tourist pages |

`Views/Shared/_StatBoxRow.cshtml` is fully data-bound (`@item.Label`) → **skip** (nothing to localize).

### Files to localize (all currently contain NO `Localizer[` calls → English-only)
**Sponsor:** Sponsor/{Index,Details,Create,Edit,Delete}; SponsorPortal/{Index,Dashboard,CompleteProfile,Reports}; SponsorBranch/{Index,Create,Edit,Delete}; SponsorReward/{Index,Create,Edit,Delete}; SponsorRedemption/Index; SponsorNotification/{Index,Support,SupportDetails,_SupportPanel,_NotificationPanel}; SponsorReview/Index; SponsorApproval/Index.
**Admin:** Destination/{Index,Create,Edit,Delete,Details}; TripPlan/{Index,Create,Edit,Delete,Details}; Mission/{Index,Create,Edit,Details,Delete}; Reward/{Index,Create,Edit,Delete,Details}; Tourist/{Index,Details,Create,Edit,Delete}; Role/{ManageAccounts,Create,AssignRole}; AdminSupport/{Index,Details}; TouristSupport/{Index,Details}.
**Special (brief-confirmed not done):** Account/Reset.cshtml, Trip/Details.cshtml, TouristSupport/* (all above).
If a view is already partially localized on inspection, keep existing keys and only fill gaps (no duplicate keys).

### Execution strategy (IMPORTANT — avoids prior output-limit failure)
- Do **NOT** launch one giant agent over all files. The previous attempt's subagents hit output limits and made zero edits.
- Process in **small batches of 2–4 related view files per agent pass** (or edit directly), so each pass is small enough to complete. A natural split:
  1. Sponsor core: `Sponsor/{Index,Details}` + `SponsorBranch/Index`
  2. `Sponsor/{Create,Edit,Delete}` + `SponsorBranch/{Create,Edit,Delete}`
  3. `SponsorPortal/*` (4 files)
  4. `SponsorReward/*` (4) + `SponsorRedemption/Index`
  5. `SponsorNotification/*` (5) + `SponsorReview/Index` + `SponsorApproval/Index`
  6. `Destination/{Index,Create,Edit,Delete,Details}`
  7. `TripPlan/{Index,Create,Edit,Delete,Details}`
  8. `Mission/{Index,Create,Edit,Details,Delete}`
  9. `Reward/{Index,Create,Edit,Delete,Details}` + `Tourist/{Index,Details}`
  10. `Tourist/{Create,Edit,Delete}` + `Role/{ManageAccounts,Create,AssignRole}`
  11. `AdminSupport/*` + `TouristSupport/*`
  12. `Account/Reset` + `Trip/Details` + `Shared/_ReviewsCarousel`
- For each batch, the agent edits only `.cshtml` files AND records every new key to a fragment file at `C:\Users\Mohamed Esam\AppData\Local\Temp\kilo\resx-frag\batchN.txt` in format `Key|English|Arabic` (one per unique key). Then a final merge step appends all fragments into BOTH resx before `</root>` and verifies en/ar key parity.
- Arabic: use Modern Standard Arabic, mirroring terminology already in `SharedResource.ar.resx` (الوجهات=Destinations, الرعاة=Sponsors, الفروع=Branches, المهام=Missions, الإدارة=Administration, نشط=Active, معلق=Pending, غير نشط=Inactive, مكتمل=Completed, ملغى=Cancelled, مسودة=Draft, مقبول=Approved, مرفوض=Rejected, الدعم=Support, الإشعارات=Notifications, البوابة=Portal, التقارير=Reports, المراجعة=Review, الاسترداد=Redemption).

---

## Validation
1. `dotnet build` in `Tourist_Project_MVC-main/Tourist_Project_MVC`. Confirm **0 errors** and **no new `MSB3568`** (total warnings ~77; pre-existing CS* nullability warnings OK).
2. Grep `Localizer[` across `Views/**` — every listed back-office file now contains `@Localizer` calls.
3. Verify en/ar resx have identical key sets (every new `en` key has an `ar` match; exactly 7 `Features_Feature*` keys each; no `Feature1_Title` duplicate).
4. Acceptance: switch language dropdown to Arabic; load every back-office/sponsor/admin page plus `Account/Reset` and `Trip/Details` — UI text + RTL (`dir="rtl"` from `_Layout`) flip to Arabic. DB-driven entity data/status values may remain English by design.

## Risks
- **Arabic quality** — produce faithful MSA; reuse existing resx terminology.
- **Status labels** — localize only the visible label, keep RAW DB string in the `switch`/comparison.
- **Resx sync** — never leave an `en` key without its `ar` counterpart (causes missing-string fallback, not a build error, but breaks acceptance).
- **Output limits** — keep each agent pass small (≤4 files); merge resx in a dedicated final step.
