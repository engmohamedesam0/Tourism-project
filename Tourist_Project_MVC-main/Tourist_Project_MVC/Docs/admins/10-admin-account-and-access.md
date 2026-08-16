---
title: Admin Account & Access
description: Understand how Admin authentication works, which permissions Admins have, and which features are restricted.
order: 10
category: For Admins
---

# Admin Account & Access

The Admin role is the highest level of access on EGYXPLORE. This guide explains how Admin authentication works, what the role can do, and which features are reserved for Admins.

## How Admin authentication works

- Sign-in uses the standard EGYXPLORE authentication (ASP.NET Identity with a secure cookie). You log in with the email and password of your Admin account.
- The platform recognizes you by the **Admin** role assigned to your account — not by a separate login screen.
- A default Admin account is provided with the sample data (`admin@egyxplore.com`). Additional Admins are created by promoting an existing account through the **Accounts** page or the AI assistant.

## Creating or changing Admin accounts

1. Open the **Accounts** page (from the admin menu).
2. Find the account you want to promote.
3. Change its role to **Admin** and save.
4. The account now has full administrative access on its next sign-in.

> **Note:** An Admin can never remove the Admin role from their own account, and cannot delete their own account — the platform always protects at least one active Admin.

## Admin-only permissions

Features that only the **Admin** role can use:

- **Admin Dashboard** — the live statistics and ArcGIS sync actions.
- **Destinations** — add, edit, and delete destinations (tourists and sponsors can only view them).
- **Missions** — create, edit, and delete missions.
- **Sponsors** — edit sponsor registry information and approve or reject new sponsors.
- **Rewards** — create, edit, and delete rewards for any sponsor.
- **Support Inbox** — view and respond to all support tickets.
- **Utilities** — create, edit, and delete utility records.
- **Trip Plans** — create, edit, and delete curated itineraries.
- **Accounts** — change user roles and delete login accounts.
- **Tourists** — view, create, edit, and delete tourist records.

## Features restricted to Admins

Anything that changes the platform's data is Admin-only:

- Writing to **ArcGIS** (creating, updating, or deleting map features) is only possible through Admin actions.
- Approving sponsor registrations.
- Responding to the platform's support queue.
- Changing a user's role.

## What tourists and sponsors cannot do

- **Tourists** — browse destinations, plan trips, complete missions, earn rewards, and contact support. They cannot see or change other users' data, manage content, or access the Admin Dashboard.
- **Sponsors** — manage their own branches, rewards, and redemptions from their portal. They cannot edit other sponsors, approve accounts, or access Admin features.

> **Note:** The Docs page follows the same rules — the For Admins documentation is visible only to Admin accounts, so it never leaks operational details to tourists or sponsors.
