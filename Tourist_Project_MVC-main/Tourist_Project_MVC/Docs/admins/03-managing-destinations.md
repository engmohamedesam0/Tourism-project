---
title: Managing Destinations
description: Add, edit, and delete destinations, manage images and GIS coordinates, and make sure tourists see correct information.
order: 3
category: For Admins
---

# Managing Destinations

Destinations are the heart of EGYXPLORE — every tourist experience (Explore, Trip Planner, Near Me, missions) revolves around them. This guide covers adding, editing, and deleting destinations, managing their media and GIS information, and verifying how they appear to tourists.

## What it is

A destination is a place tourists can explore — a temple, museum, beach, or public site — with a name, description, images, ticket information, and an exact location on the map. Destinations are stored both in the website database and in ArcGIS; ArcGIS is the source of truth, and the two are kept in sync automatically.

## Why use it

- Keep the catalog fresh with new and updated places.
- Fix incorrect descriptions, prices, hours, or map positions.
- Remove outdated or inactive destinations.

## Viewing destinations

1. Open the **Destinations** page from the admin menu (or go to `/Destination/Index`).
2. The smart table combines website data and ArcGIS records in one view.
3. Use **search**, **status** (Active / Pending / Inactive), and **category** filters, plus sorting, to find what you need.

## Adding a new destination

1. Open the **Admin Dashboard** and click **Add Destination** (or go to `/AdminDashboard/Destinations/Add`).
2. Fill in the **English name**, **Arabic name** (optional), **city**, and **category**.
3. Add a **description** and optional **tags**.
4. Set **ticket details** — whether a ticket is required and the prices (Egyptian, student, foreign). If the category is **Public**, ticket fields are cleared automatically and the destination is treated as free.
5. Add **images** — either upload image files or paste external image URLs.
6. **Select the location on the map** — this sets the latitude and longitude (GIS coordinates) used everywhere on the site.
7. Submit the form. The destination is created in ArcGIS first, then synchronized to the website database.

> **Note:** If the map location is not selected, the form will not submit — the location is required.

## Editing a destination

1. On the **Destinations** page, click **Edit** on the row you want to change.
2. Update any field — name, description, category, status, prices, opening and closing hours, images, and the **latitude/longitude**.
3. Click **Save**. The change is pushed to ArcGIS first, then synchronized to the database.
4. The destination's detail page and map position update immediately.

> **Note:** Latitude must be between -90 and 90, and longitude between -180 and 180. Validation errors are highlighted before anything is saved.

## Deleting a destination

1. On the **Destinations** page, click **Delete** on the row you want to remove.
2. Review the confirmation screen — deleting also removes the destination's missions and trip stops, and detaches its reviews and favorites.
3. Confirm the deletion. The destination is removed from ArcGIS first; only after the map service confirms removal is it deleted from the database.

> **Caution:** Deletion is permanent and affects other features (missions tied to the destination disappear). Double-check before confirming.

## Managing images

- **Uploaded images** are stored under `/uploads/destinations` and appear automatically on the destination page.
- **External URLs** must be valid absolute http(s) links; each URL is validated before saving.
- You can mix uploaded files and external URLs in one destination.

## Making sure tourists see it correctly

1. After adding or editing, open the **Explore** page as a tourist would.
2. Search for the destination and open its detail page — check the name, description, images, prices, and hours.
3. Check the **map position** on the detail page and in the Trip Planner.
4. If anything looks out of date, return to the **Destinations** page, edit the record, and save again.

> **Tip:** Keep the category and status accurate — only **Active** destinations are shown to tourists in normal browsing.
