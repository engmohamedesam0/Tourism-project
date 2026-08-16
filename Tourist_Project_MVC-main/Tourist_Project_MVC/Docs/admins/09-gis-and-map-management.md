---
title: GIS & Map Management
description: Manage locations and coordinates through ArcGIS, keep the map in sync, and understand how GIS data powers the tourist experience.
order: 9
category: For Admins
---

# GIS & Map Management

Every destination and branch on EGYXPLORE has a precise location on the map. Locations are managed through the platform's **ArcGIS** integration, which acts as the authoritative map service.

## What it is

- **GIS data** — each destination and branch stores a geographic location (latitude and longitude, SRID 4326) plus map attributes such as name, category, status, and images.
- **ArcGIS** — the online map service that stores these features and powers every map view on the website.
- **Sync** — the two-way process that keeps the website database and ArcGIS consistent.

## Why use it

- Tourists rely on accurate map positions for Explore, the Trip Planner, and Near Me.
- One consistent location record prevents the website and map from disagreeing.

## How locations and coordinates are managed

### Adding a location

1. When creating a destination, **select the location on the map** in the Add Destination form.
2. The form stores the chosen **latitude** and **longitude** — these become the destination's coordinates.
3. The destination is created in ArcGIS and then synchronized to the website.

### Editing a location

1. Open the destination's **Edit** page.
2. Update the **latitude** and **longitude** fields (latitude between -90 and 90, longitude between -180 and 180).
3. Save — the new coordinates are pushed to ArcGIS first and then to the database.
4. The map position updates everywhere immediately.

### Branch and utility locations

- **Branches** — sponsor branch locations are managed by sponsors from their portal; they follow the same ArcGIS pipeline.
- **Utilities** — each utility (police, fire, hospital, pharmacy) also stores a map location, set when you create or edit it on the **Utilities** page.

## Keeping the map in sync

1. Open the **Admin Dashboard**.
2. Use **Sync to ArcGIS** to push the website's destinations, branches, tourists, and redemptions to the map feature layers.
3. Use **Sync from ArcGIS** to pull destination data changed directly on the map service back into the website.
4. The result message tells you exactly how many records were added or updated.

> **Note:** Destinations are always written to ArcGIS first — if the map service is unavailable, destination changes cannot be saved.

## How GIS affects destinations and the tourist experience

- **Explore** — tourists browse destinations on the map and in lists; the map markers come from the GIS data.
- **Near Me** — distances are calculated from real coordinates, so tourists see the closest sponsors and destinations around them.
- **Trip Planner** — itinerary stops use the same locations, so routes make geographic sense.
- **Admin Dashboard** — the live dashboard renders the platform's statistics directly from ArcGIS.

> **Tip:** After any batch of location changes, run **Sync to ArcGIS** and then spot-check a destination's position on the Explore map to confirm everything is aligned.
