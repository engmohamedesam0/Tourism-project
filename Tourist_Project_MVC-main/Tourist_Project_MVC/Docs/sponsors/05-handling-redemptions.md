---
title: Handling Redemptions
description: Review, approve, and track tourist reward redemptions via SponsorRedemptionController.
order: 5
---

# Handling Redemptions

When a tourist redeems a reward, the event is recorded in the `Redemptions` table and surfaced in **SponsorRedemptionController** for action.

## Reviewing redemptions

1. Open **Redemptions** from the sponsor navbar or click the badge in the notification bell.
2. Each entry shows the tourist name, reward title, branch, timestamp, and status.
3. Filter by status (Pending, Approved, Rejected) or date range to focus on recent requests.

## Approval workflow

- **Pending** — review and confirm the tourist visited or qualified for the reward.
- **Approved** — the tourist is notified and the reward is marked as used.
- **Rejected** — optionally add a reason so the tourist understands why.

> **Warning:** Rejecting a redemption without explanation can damage your sponsor rating. Use the reason field when rejecting.

## Tracking history

- Approved and rejected redemptions remain in history for audit and reporting.
- Export redemption logs from the Reports section for monthly reconciliation.

> **Tip:** Respond to pending redemptions within 24 hours to maintain a high reputation score with tourists.
