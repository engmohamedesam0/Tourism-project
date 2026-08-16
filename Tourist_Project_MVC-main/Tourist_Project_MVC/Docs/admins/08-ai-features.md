---
title: AI Features
description: Use the AI assistant to get platform statistics, manage accounts, and create or update rewards and destinations.
order: 8
category: For Admins
---

# AI Features

EGYXPLORE includes an **AI assistant** that is aware of who is talking to it. When you are signed in as an Admin, the assistant gains a set of admin-only tools that let you manage the platform by typing a request in plain language.

## What it is

The AI assistant is the chat widget (sparkles button) at the bottom-right of the screen. It can answer questions about the platform and, for Admins, perform real administrative actions using the same services and permission rules as the website itself.

## What the AI can do for Admins

- **Platform overview** — ask "give me a platform overview" and it reports counts of tourists, sponsors, destinations, branches, rewards, redemptions, completed missions, reviews, and the average rating.
- **List users and sponsors** — ask who is registered, optionally filtered by role, or ask for the sponsor list to use in later steps.
- **Change a user's role** — promote or demote an account between User, Sponsor, and Admin.
- **Manage rewards** — create, update, or delete rewards (with a chosen sponsor, points, quantity, and expiration date).
- **Manage destinations** — create, update, or delete destinations, including names, descriptions, prices, images, and coordinates.

## How to use it

1. Click the **sparkles** button at the bottom-right of any page.
2. Type your request in plain language — for example, *"Create a reward called Summer Explorer for the El Fishawy sponsor, 200 points, expiring next month."*
3. Review the **confirmation summary** the assistant shows before any change is applied.
4. Confirm — the action runs and the assistant reports the result.

> **Note:** Every admin action is performed with your confirmation first. Nothing is changed without your approval.

## Important limitations and requirements

- **Destinations require ArcGIS** — creating, updating, or deleting a destination goes through the map service. If ArcGIS is unreachable, the action fails and nothing is saved.
- **You cannot change your own role** — the assistant will refuse to demote or promote the account you are signed in with.
- **Roles are limited** to User, Sponsor, and Admin.
- **Rewards need a sponsor** — the assistant may ask you to list sponsors first so it can attach the reward to the right business.
- **AI availability** depends on the configured AI provider (Gemini primary, with an OpenAI fallback). If the provider reports quota exhaustion, the assistant may be temporarily unavailable.

> **Tip:** The AI assistant is a shortcut, not a replacement for the management pages — the guides in this section always describe the click-through path as well.
