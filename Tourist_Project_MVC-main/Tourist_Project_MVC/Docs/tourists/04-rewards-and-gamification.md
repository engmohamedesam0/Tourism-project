---
title: Rewards & Gamification
description: Track XP, level up, earn badges, and redeem real-world rewards from sponsors.
order: 4
---

# Rewards & Gamification

EGYXPLORE turns exploration into a progression system. Every meaningful action — visiting a destination, completing a mission, or leaving a review — awards **XP** and can unlock **Badges**.

## XP and levels

Your tourist profile accumulates XP through platform activity. As XP increases, you advance through levels tracked in `UserProgress` and rendered by **TouristRewardController**:

- Higher levels unlock privileges such as priority support and higher redemption caps.
- The XP bar in **Rewards** updates automatically after eligible activities.

### Earning XP

| Activity | XP |
|----------|----|
| Visit a destination | +10 |
| Complete a mission | +25 |
| Write a review | +15 |
| Bookmark a destination | +5 |

> **Tip:** Daily login streaks grant bonus XP. Check your reward dashboard often to track progress.

## Badges

Badges are tiered achievements stored in `UserBadges` with rarity levels:

- **Common** — easy, repeatable actions.
- **Rare** — visiting multiple governorates or completing challenging missions.
- **Epic** — event-driven or timeline milestones.
- **Legendary** — one-of-a-kind accomplishments.

Unlocking a badge triggers a toast notification on your next page load.

## Redeeming rewards

Sponsors publish rewards in **TouristReward/Index**:

1. Browse available rewards and filter by sponsor or points cost.
2. Click **Redeem** to generate a redemption record in the `Redemptions` table.
3. Present the confirmation at any participating branch.

> **Warning:** Expired rewards cannot be reinstated. Always check validity dates before visiting a branch.
