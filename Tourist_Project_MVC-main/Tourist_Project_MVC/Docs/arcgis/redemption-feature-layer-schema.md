# Redemptions Feature Layer — Schema & Data Export

Non-spatial hosted **table** — one row per reward redemption. Exists so the ArcGIS dashboard can answer *"which single reward gets redeemed the most, across everyone?"* (top reward by redemption count, optionally broken down by branch or tourist).

- **Data file:** [`redemptions-layer-data.csv`](redemptions-layer-data.csv) — 18 rows, one per `Redemptions` record, joined to `Rewards.Title`.
- **Source:** `Redemptions` table (Models/Redemption.cs). Business key `RedemptionId` mirrors how `TouristId` keys the tourists table.

---

## 1. Field schema

| # | Field | Alias | ArcGIS type | Nullable | Source column | Notes |
|---|-------|-------|-------------|----------|---------------|-------|
| 1 | `RedemptionId` | Redemption ID | Integer | No | `Redemptions.Id` | Business key. ArcGIS auto-adds its own `OBJECTID`. |
| 2 | `RewardTitle` | Reward title | String (128) | No | `Rewards.Title` | Drives the "top reward" aggregation. |
| 3 | `TouristId` | Tourist ID | Integer | No | `Redemptions.TouristId` | Link back to the tourist. |
| 4 | `BranchId` | Branch ID | Integer | Yes | `Redemptions.BranchId` | Null for branch-less redemptions. |
| 5 | `PointsRedeemed` | Points redeemed | Integer | No | `Redemptions.PointsRedeemed` | |
| 6 | `RedemptionDate` | Redemption date | Date | No | `Redemptions.RedemptionDate` | ISO `yyyy-MM-dd` in the CSV. |
| 7 | `Status` | Status | String (32) | Yes | `Redemptions.Status` | e.g. `Active`, `Used`. |

**Layer type:** this is a **non-spatial table** — it has no geometry columns.

---

## 2. Publishing in ArcGIS Online

1. **Content → New item → CSV** → upload `redemptions-layer-data.csv`.
2. Choose **Add data as a hosted table**.
3. Verify field types against the table above (CSV import auto-detects; check `RedemptionId`, `RedemptionDate`, `BranchId`).
4. Publish. Note the **FeatureServer URL** — add it to `appsettings.json`:

```jsonc
"ArcGIS": {
  "ApiKey": "...",
  "RedemptionsTableUrl": "https://services3.arcgis.com/<ORG>/arcgis/rest/services/<NAME>/FeatureServer"
}
```

5. Share the table (at least to your org) so the Experience Builder dashboard can read it.

---

## 3. Sync integration

The database is the **source of truth** → this table is **push-only** (DB → ArcGIS):

- `IArcGISSyncService.SyncRedemptionsAsync()` — modeled on `SyncTouristsTableAsync`: reads all `Redemptions` (joined with `Rewards.Title`) and does a full add/update/delete refresh keyed on `RedemptionId`.
- Wired into `AdminDashboardController.SyncToArcGIS` (the "Sync with ArcGIS" dropdown action) alongside destinations / branches / tourists / nationality.
- **Do not** run a pull-sync for this table: the existing pull path deletes local rows missing from ArcGIS, which would destroy redemption history if the table is partial.
- The sync is a no-op (returns success) while `RedemptionsTableUrl` is empty — safe to push before the table is published.

---

## 4. Data summary (as exported)

- 18 redemption rows; `RedemptionId` 1–18.
- Statuses: `Active` (9), `Used` (9).
- Redemption dates range 2026-07-05 → 2026-10-15; points redeemed 50–500.
- `BranchId` populated on all 18 rows (1–11).
- **Note:** with the current seed data every reward title has exactly **one** redemption (a tie). The "top reward" chart in the dashboard will show equal bars until more redemptions accrue — the aggregation itself is ready to rank them the moment counts differ.
