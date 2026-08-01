---
title: Managing Branches
description: Create, edit, and sync sponsor branches with SponsorBranchController and ArcGIS.
order: 3
---

# Managing Branches

Sponsor branches are physical or virtual locations where tourists can redeem rewards. **SponsorBranchController** handles the full CRUD lifecycle and keeps the ArcGIS map in sync.

## Adding a branch

1. Open **Branches** from the sponsor navbar.
2. Click **Create** and enter the branch name, address, contact details, and location on the map.
3. Save the branch. It appears in both the branch list and the ArcGIS map layer.

## Editing and deleting

- Update operating hours, contact info, or map position at any time.
- Delete a branch only after confirming that no active rewards depend on it.

### ArcGIS sync

- Every branch update pushes to ArcGIS Online through the sync service.
- Changes can take a few minutes to appear on public map views.
- If a branch does not show up, refresh the map or use the sync trigger in the branch edit form.

> **Warning:** Removing a branch does not automatically deactivate associated rewards. Update reward availability manually to avoid confusing tourists.
