---
title: Admin Dashboard
description: Access your command center, read the statistics, and keep map data in sync with ArcGIS.
order: 1
category: For Admins
---

# Admin Dashboard

The **Admin Dashboard** is your command center for the whole EGYXPLORE platform. It brings together live statistics, ArcGIS map data, and the one-click sync actions that keep the website and the map service aligned.

## What it is

The dashboard is a live, map-powered overview of everything happening on EGYXPLORE — tourist activity, sponsor performance, destination reach, and engagement with missions and rewards. It replaces manual guesswork with a single screen you can check at any time.

## Why use it

- Get a platform-wide health check at a glance (how many tourists, sponsors, destinations, and redemptions there are).
- Spot trends — which destinations are most visited, how missions and rewards are performing.
- Trigger the ArcGIS sync actions that keep map data consistent between the website and the map service.

## How to access it

1. Sign in with an **Admin** account.
2. Click **Admin Dashboard** in the primary navigation bar.
3. The dashboard opens directly at `/AdminDashboard`.

> **Note:** The dashboard is restricted to Admin accounts. Tourists and Sponsors cannot see it.

## What each section represents

- **Live map and statistics panel** — an ArcGIS-powered view of the platform's data: tourist counts, sponsor counts, destinations, branches, rewards, redemptions, completed missions, and review ratings.
- **Sync to ArcGIS** — pushes the current destination, branch, tourist, and redemption data from the website to the ArcGIS feature layers. Use this after you add or update destinations locally.
- **Sync from ArcGIS** — pulls the latest destination data from ArcGIS back into the website database. Use this after data was changed directly on the map service.

## Understanding the data

- Numbers reflect the live database, so they change as tourists register, complete missions, redeem rewards, and leave reviews.
- If a figure looks stale, reload the page. Real-time push updates are not supported in this version.
- The dashboard depends on the ArcGIS service being reachable. If the map service is offline, statistics and sync actions may be unavailable.

## Keeping the data in sync

1. Make your changes (for example, add a destination or update branch locations).
2. Open the **Admin Dashboard**.
3. Click **Sync to ArcGIS** to push website data to the map service, or **Sync from ArcGIS** to pull map-service changes back into the website.
4. Confirm the message at the top of the page — it reports how many records were added or updated.

> **Tip:** Sync after every batch of destination or branch changes so tourists always see the same information on the map, in Explore, and in Near Me.
