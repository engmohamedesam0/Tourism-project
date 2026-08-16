---
title: Missions & Gamification
description: Create and manage missions, rewards, levels, and badges — and understand how they shape the tourist experience.
order: 4
category: For Admins
---

# Missions & Gamification

Gamification is what makes EGYXPLORE engaging: tourists complete **missions** to earn **points**, which unlock **levels**, **badges**, and **rewards**. As an Admin you control the missions and rewards that drive this loop.

## What it is

- **Missions** — tasks attached to destinations (for example, visiting a site or exploring a city) that award points when completed.
- **Rewards** — real-world perks offered by sponsors that tourists redeem with their points.
- **Levels and badges** — automatic recognition of a tourist's progress, earned from accumulated points and achievements.

## Why use it

- Keep the experience lively with a steady stream of new missions.
- Balance the economy — mission points should match the cost of rewards.
- Highlight destinations you want tourists to visit.

## Managing missions

### Viewing missions

1. Open the **Missions** page from the admin menu (or go to `/Mission/Index`).
2. The overview shows the total mission count, destinations covered, average points reward, and mission types, plus the full list.
3. Use **search** and the **mission type** filter to find specific missions.

### Creating a mission

1. On the **Missions** page, click **Create**.
2. Enter a **title** and **description**.
3. Choose the **mission type**.
4. Set the **points reward** — how many points a tourist earns for completing it.
5. Select the **destination** the mission is attached to.
6. Save the mission. It becomes visible to tourists immediately and appears in the mission feed.

> **Note:** A mission must belong to a destination. If you delete that destination, its missions are removed as well.

### Editing and deleting missions

1. On the **Missions** page, click **Edit** on a mission to change its title, description, type, points, or destination, then save.
2. Click **Delete** to remove a mission. Tourists who already completed it keep their earned points.

## Managing rewards

1. Open the **Rewards** page (or go to `/Reward/Index`) to see all rewards, active counts, redemptions, and average points required.
2. Click **Create** to add a reward:
   - **Title**, **type** (for example Discount, Voucher, Gift), and **description**.
   - **Points required** — the cost in tourist points.
   - **Quantity available** and **expiration date**.
   - The **sponsor** offering the reward.
3. Save the reward — it immediately appears in the tourist Rewards catalog.
4. Use **Edit** to change details or set **status** (Active, Paused, Removed), and **Delete** to remove a reward entirely.

> **Tip:** A reward with no sponsor or with `quantity 0` will not be redeemable. Double-check both before publishing.

## Levels and badges

Levels and badges are **earned automatically** by the gamification engine as tourists gain points and complete activities — there is no manual badge editor. You influence them indirectly:

- Set meaningful **points rewards** on missions so progress feels rewarding.
- Keep **rewards** priced so points are worth collecting.
- A tourist's current level and badge appear in their navigation bar and profile, powered by their accumulated progress.

## How it connects to the tourist experience

1. A tourist opens a destination and finds a **mission** attached to it.
2. They complete the mission and instantly receive its **points**.
3. Points accumulate into **levels and badges**, which are shown on their profile and next to their name.
4. Tourists spend points in the **Rewards** catalog to redeem sponsor perks — completing the loop.

> **Note:** Reward redemptions are handled by the sponsor side (see the For Sponsors documentation). As an Admin, you manage the mission and reward catalog that feeds the whole system.
