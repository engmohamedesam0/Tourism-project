---
title: Managing Website Content
description: Understand every type of content on EGYXPLORE, where it appears on the website, and how to create, edit, and delete it.
order: 7
category: For Admins
---

# Managing Website Content

EGYXPLORE is built from several content types, each with its own management area and its own place on the website. This guide maps them out so you always know where to go.

## Content types and where they appear

| Content type | Managed from | Where tourists see it |
| --- | --- | --- |
| **Destinations** | **Destinations** page | Explore, destination detail pages, Trip Planner, Near Me, missions |
| **Trip Plans** | **Trip Plans** page | Trip Planner (admin-curated itineraries) |
| **Missions** | **Missions** page | Destination pages and the mission feed |
| **Rewards** | **Rewards** page | Tourist Rewards catalog |
| **Sponsors** | **Sponsors** page | Sponsor profiles and Near Me |
| **Utilities** | **Utilities** page | Near Me (police, fire, hospitals, pharmacies) |
| **Accounts** | **Accounts** page | Logins and roles across the whole platform |

## Managing destinations

1. Open the **Destinations** page.
2. Click **Add Destination** on the **Admin Dashboard** to create a new one.
3. Click **Edit** or **Delete** on any row to update or remove it.
4. Every change is written to ArcGIS first and then synchronized to the website. See the Managing Destinations guide for full steps.

## Managing trip plans

Trip Plans are curated itineraries published for the community:

1. Open the **Trip Plans** page.
2. Click **Create** to build a new plan — give it a name and select the destinations it includes.
3. Use **Edit** to change the plan or its stops, and **Delete** to remove it.
4. Published plans appear in the Trip Planner for tourists.

## Managing missions and rewards

1. **Missions** — create, edit, and delete from the **Missions** page; each mission is tied to a destination and awards points.
2. **Rewards** — create, edit, and delete from the **Rewards** page; each reward belongs to a sponsor and costs points.
3. See the Missions & Gamification guide for detailed steps.

## Managing sponsors and utilities

1. **Sponsors** — add or update sponsor registry data from the **Sponsors** page, and handle approvals from the **Approvals** page.
2. **Utilities** — create, edit, and delete essential facilities (police stations, fire stations, hospitals, pharmacies) from the **Utilities** page; every utility needs a name, type, address, and map location.

## Managing accounts

1. Open the **Accounts** page.
2. Every login account is listed with its current role (**User**, **Sponsor**, or **Admin**).
3. Change a user's role with the role selector, or delete an account (you cannot delete your own).
4. Deleted accounts lose login access, but their tourist or sponsor history is preserved.

> **Note:** Content you create becomes visible to tourists almost immediately after saving — always review new content (especially descriptions, prices, and map positions) before publishing.

## Keeping content consistent

- Use **status** fields (for example, Active / Inactive on destinations) to temporarily hide content without deleting it.
- After large batches of changes, open the **Admin Dashboard** and run **Sync to ArcGIS** so the map service matches the website.
- Check the tourist-facing pages (Explore, Trip Planner, Near Me, Rewards) after publishing to confirm everything displays correctly.
