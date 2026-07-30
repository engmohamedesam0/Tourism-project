---
title: Near Me
description: Find nearby sponsors and destinations with the spatial Near Me search.
order: 3
---

# Near Me

The **Near Me** page reveals sponsors and destinations nearest to your current or saved location. Results are ranked by real-world distance using PostGIS spatial queries through **NearMeController**.

## How proximity search works

- The platform queries the `Destination` and `Branch` tables using `ST_Distance` on latitude/longitude columns.
- Results are sorted from closest to farthest, with an approximate travel distance displayed for each entry.
- You can tap any result to open its detail page, see sponsor reviews, and access rewards.

## Filtering sponsor types

- Narrow results by category (cafe, museum, tours, etc.) to find exactly what you need.
- Combine text search with distance ranking to locate amenities near a specific landmark or hotel.

### Saving favorite locations

- Bookmark a branch or destination by visiting its page; bookmarked items appear in your profile.
- Saved items are excluded from duplicates when you view your trip planner.

> **Tip:** Enable location services in your browser for the most accurate distance calculations. If you decline, the platform falls back to the location stored in your profile.
