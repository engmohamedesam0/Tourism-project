---
title: Trip Planning
description: Build and manage multi-stop itineraries with the EGYXPLORE trip planner.
order: 2
---

# Trip Planning

EGYXPLORE lets you build, save, and share multi-stop itineraries. The trip planner is powered by **TripController** and surfaces curated plans from **TripPlanController** that Admins publish for the community.

## Building a trip

1. Open **Trip** from the tourist navbar.
2. Add destinations from **Explore** or **Near Me** to your itinerary.
3. Reorder stops by dragging them in the list.
4. Your trip auto-saves as you edit.

## Map preview

- Your itinerary is rendered on the ArcGIS map widget so you can visualize the route.
- Distances between stops are calculated using PostGIS spatial functions that respect real-world geography.
- You can open any destination directly from the map to see its details.

### Sharing your trip

- Export a summary to share with friends or travel companions.
- Trips remain private to your account until you choose to export them.

> **Tip:** Plan trips during off-peak seasons for quieter sites and better reward redemption availability.

## Admin-curated trip plans

Admins use **TripPlanController** to publish read-only trip plans with difficulty ratings and ordered destination lists:

- You can fork a published plan into your own editable trip.
- Cloning creates a full copy in your account so you can customize stops without affecting the original.

> **Note:** If you fork a shared plan, your version is independent — later changes made by the Admin will not overwrite yours.
