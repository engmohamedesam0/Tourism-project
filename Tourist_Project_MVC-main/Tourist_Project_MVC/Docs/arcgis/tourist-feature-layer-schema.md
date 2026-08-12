# Tourists Feature Layer — Schema & Data Export

Combines the `AspNetUsers` (Identity) and `Tourists` tables into **one flat ArcGIS feature-layer row per tourist**, following the same pattern as the existing Destination / Branch layers (`ArcGISSyncService` + `applyEdits`).

- **Data file:** [`tourists-layer-data.csv`](tourists-layer-data.csv) — 13 rows, one per tourist with a linked login account.
- **Excluded:** users without a `Tourists` record (currently the sponsor `elfishawy@egyxplore.com`). The layer represents tourists only.

---

## 1. Field schema

| # | Field | Alias | ArcGIS type | Length | Nullable | Source column | Notes |
|---|-------|-------|-------------|--------|----------|---------------|-------|
| 1 | `TouristId` | Tourist ID | Integer | — | No | `Tourists.Id` | Business key. ArcGIS auto-adds its own `OBJECTID`. |
| 2 | `UserId` | User ID | String | 128 | No | `AspNetUsers.Id` | Link back to the login account. |
| 3 | `Email` | Email | String | 256 | No | `AspNetUsers.Email` | PII — keep the layer private. |
| 4 | `FirstName` | First name | String | 128 | No | `AspNetUsers.FirstName` | |
| 5 | `LastName` | Last name | String | 128 | No | `AspNetUsers.LastName` | |
| 6 | `FullName` | Full name | String | 256 | No | computed | `FirstName + " " + LastName` |
| 7 | `Nationality` | Nationality | String | 128 | Yes | `AspNetUsers.Nationality` | Powers per-nationality analytics. |
| 8 | `PhoneNumber` | Phone number | String | 64 | Yes | `AspNetUsers.PhoneNumber` | PII. |
| 9 | `IdNumber` | National ID | String | 64 | Yes | `Tourists.IdNumber` | PII — sensitive. |
| 10 | `Passport` | Passport | String | 64 | Yes | `Tourists.Passport` | PII — sensitive. |
| 11 | `PointBalance` | Points balance | Integer | — | No | `Tourists.point_Balance` | |
| 12 | `RegisterDate` | Register date | Date | — | Yes | `Tourists.RegisterDate` | ISO `yyyy-MM-dd` in the CSV. |
| 13 | `Status` | Status | String | 32 | Yes | `Tourists.Status` | e.g. `Active`. |

**Layer type:** this is a **non-spatial table** — it has no geometry columns. Tourists have no coordinates in the database, and per request the `Latitude` / `Longitude` (plus the notification flags, language/interests, and profile picture) fields were removed from the schema. If you later need tourists on a map, add coordinate columns at that point.

---

## 2. Publishing in ArcGIS Online

1. **Content → New item → CSV** → upload `tourists-layer-data.csv`.
2. Choose **Add data as a hosted table**.
3. Verify field types against the table above (CSV import auto-detects; adjust `Email`, dates, etc. if needed).
4. Publish. Note the **FeatureServer URL** — add it to `appsettings.json`:

```jsonc
"ArcGIS": {
  "ApiKey": "...",
  "TouristsLayerUrl": "https://services3.arcgis.com/<ORG>/arcgis/rest/services/Tourists/FeatureServer"
}
```

---

## 3. Sync integration (mirror of the destinations pattern)

The database is the **source of truth** for users → make this layer **push-only** (DB → ArcGIS):

- Add `SyncTouristsAsync(IEnumerable<Tourist>)` to `IArcGISSyncService`, modeled on `SyncBranchesAsync` — build each feature's attributes by joining `Tourist.ApplicationUser` (FirstName, LastName, Email, Nationality, PhoneNumber).
- Hook the push where tourists are created/updated (registration, admin tourist create/edit), like `SponsorBranchController` does for branches.
- Extend `AdminDashboardController.SyncToArcGIS` to also push tourists.
- **Do not** run a pull-sync for this layer: the existing pull path deletes local rows missing from ArcGIS, which would delete user accounts if the layer is partial.
- Expose the layer URL from `MapController` for `maps.js` if it becomes spatial later.

---

## 4. Data summary (as exported)

- 13 tourist rows; join key `Tourists.ApplicationUserId = AspNetUsers.Id`.
- Nationalities present: Egyptian (4), German (2), American, Japanese, British, Chinese, French, Emirati, Italian.
- Register dates range 2026-01-05 → 2026-08-10; all statuses `Active`.
- Point balances 0–500.
