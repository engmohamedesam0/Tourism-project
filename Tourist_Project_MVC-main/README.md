# EGYXPLORE — Egyptian Tourist Experience Platform

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-10-512BD4?logo=dotnet)](https://learn.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16+-4169E1?logo=postgresql)](https://www.postgresql.org/)
[![PostGIS](https://img.shields.io/badge/PostGIS-Spatial-4169E1)](https://postgis.net/)
[![ArcGIS](https://img.shields.io/badge/ArcGIS-Online-006B8F)](https://www.arcgis.com/)
[![License: Proprietary](https://img.shields.io/badge/License-Proprietary-red.svg)](LICENSE)

A full-stack **graduation project** web platform that connects **tourists**, **sponsors** (hotels, airlines, cafés, restaurants, travel agencies), and **admins** around tourism in Egypt — with interactive maps powered by **ArcGIS Online**, AI-powered assistants, gamification (missions & rewards), and a bilingual (English / Arabic) experience.

---

## Table of Contents

- [✨ Features](#-features)
- [🧰 Tech Stack](#-tech-stack)
- [📁 Project Structure](#-project-structure)
- [🚀 Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Database setup](#database-setup)
  - [Configuration](#configuration)
  - [Run](#run)
- [👤 Default Accounts](#-default-accounts)
- [🗺️ ArcGIS Integration](#️-arcgis-integration)
- [🤖 AI Features](#-ai-features)
- [📱 Mobile API (JWT)](#-mobile-api-jwt)
- [🌐 Localization](#-localization)
- [🧩 Data Seeding](#-data-seeding)

---

## ✨ Features

### 👤 Tourists
- **Explore & discover** destinations, sponsor branches and essential utilities across Egypt on an interactive ArcGIS map.
- **Near Me** — find the closest hotels, cafés, restaurants and services using spatial (PostGIS) queries.
- **Utilities directory** — police stations, fire stations, hospitals and pharmacies (126+ real facilities seeded).
- **Trip planning** — create and manage trip plans, save **favorites**.
- **Gamification** — complete **missions**, earn **points**, redeem **rewards** at sponsor branches.
- **AI chat assistant** — ask about destinations, trips and Egypt travel tips (Gemini / OpenAI).
- **Notifications** — real-time SignalR notifications.
- **Support tickets** — contact support and track replies.

### 🏨 Sponsors
- **Sponsor portal** — register as a sponsor, complete your business profile (choose a category: Café, Restaurant, Hotel, Airline, …).
- **Branch management** — create and edit branches; each branch automatically inherits its sponsor's category and is pushed to the ArcGIS map.
- **Rewards & missions** — publish rewards and missions for tourists.
- **AI tools** — an AI agent can draft new branches from a short description.
- **Reviews & support** — view tourist reviews and reply to tickets.

### 🛡️ Admins
- **Full management** — tourists, sponsors, branches, destinations, missions, rewards, utilities.
- **ArcGIS dashboard** — embedded **ArcGIS Experience Builder** dashboard for real-time analytics (nationality breakdowns, tourist counts, etc.).
- **One-click sync** — push the local database to ArcGIS feature layers (add new / update changed / delete stale) and pull destinations back; result counts are shown in a toast.
- **Approvals** — approve sponsor registration requests.
- **Support inbox** — manage all support tickets.

---

## 🧰 Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core **10** (MVC), C#, Minimal hosting |
| Data access | EF Core **10** + **Npgsql** (PostgreSQL), **PostGIS** + **NetTopologySuite** for spatial data |
| Auth | ASP.NET Core **Identity** (roles: `Admin`, `Sponsor`, `User`), optional Google / Facebook OAuth, **JWT** for the mobile API |
| Realtime | **SignalR** (notification hubs) |
| Frontend | Razor views, partials & **View Components**, Bootstrap 5 + Bootstrap Icons, jQuery/AJAX, RTL support |
| Maps | **ArcGIS Online** REST (feature layers, API key), **ArcGIS Experience Builder** embed |
| AI | **Gemini** / **OpenAI** (chat + agent tools) |
| Localization | English + Arabic resource files, RTL layout switching |

---

## 📁 Project Structure

```
Tourist_Project_MVC/
├── Controllers/            # MVC controllers (web)
│   ├── MobileControllers/  # JWT-protected mobile REST API
│   └── HubNotifications/   # SignalR hubs
├── Middlewares/            # Custom middleware (e.g. user-exists check)
├── Data/                   # EF Core DbContext (TouristContext)
├── Migrations/             # EF Core migrations (auto-applied at startup)
├── Models/                 # Entities + constants (SponsorCategories, UtilityTypes, …)
├── View_Model/             # View models & DTOs
├── Repositories/           # Repository layer
├── Services/               # DbInitializer, ArcGISSyncService, AI agents,
│   │                       # gamification, notifications, support tickets, docs
│   └── AiTools/            # AI tool implementations (e.g. sponsor branch drafting)
├── SeedData/               # JSON seed data (users, destinations, branches, utilities…)
├── ViewComponents/         # UI components (nav badges, stat boxes…)
├── Resources/              # Localization .resx (en / ar)
├── Views/                  # Razor views
├── wwwroot/                # Static assets (css, js, images)
└── Tourist_Project_MVC.csproj
```

Helper files at the repository root: `arcgis-proxy.py`, CSV/seed utility scripts (see [Utilities & Scripts](#️-utilities--scripts)).

---

## 🚀 Getting Started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (or newer)
- [PostgreSQL](https://www.postgresql.org/) 14+ with the **PostGIS** extension enabled
- (Optional) An [ArcGIS Online](https://www.arcgis.com/) account + API key for full map/sync functionality
- (Optional) Gemini or OpenAI API keys for the AI features

### Database setup

1. Create the database (adjust name/credentials to match your connection string):

   ```sql
   CREATE DATABASE Tourist_PostGIS_DB_MVC;
   ```

2. Enable PostGIS on it:

   ```sql
   \c Tourist_PostGIS_DB_MVC
   CREATE EXTENSION IF NOT EXISTS postgis;
   ```

> EF Core migrations and the seed data are applied **automatically on first run** — no manual migration step needed.

### Configuration

All settings live in `appsettings.json` (or override via [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for production):

```jsonc
"ConnectionStrings": { "CS": "Host=localhost;Port=5432;Database=Tourist_PostGIS_DB_MVC;Username=postgres;Password=admin" },
"Gemini":      { "ApiKey": "", "Model": "gemini-3.6-flash" },
"OpenAI":      { "ApiKey": "", "Model": "gpt-4o-mini" },
"Jwt":         { "Key": "", "Issuer": "TouristProjectMVC", "Audience": "TouristProjectMVCMobile" },
"Authentication": { "Google": { "ClientId": "", "ClientSecret": "" }, "Facebook": { "ClientId": "", "ClientSecret": "" } },
"ArcGIS": {
  "ApiKey": "...",
  "DestinationsLayerUrl": ".../Destination_feature_layer/FeatureServer",
  "BranchesLayerUrl":     ".../Branches/FeatureServer",
  "TouristsTableUrl":     ".../tourists_layer_data/FeatureServer",
  "TouristNationalityLayerUrl": ".../tourists_nationality_layer_data/FeatureServer",
  "RedemptionsTableUrl":  "",
  "DashboardUrl":         "https://experience.arcgis.com/experience/..."
}
```

### Run

```bash
dotnet restore
dotnet run --project Tourist_Project_MVC
```

Open **http://localhost:5217** — migrations, seed data and the demo accounts are created automatically.

---

## 👤 Default Accounts

| Role | Email | Password |
|---|---|---|
| Admin | `admin@egyxplore.com` | `AdminPass123!` |
| Sponsor | `elfishawy@egyxplore.com` | `SponsorPass123!` |
| Tourist | `ahmed.hassan@egyxplore.com` | `TouristPass123!` |

> 🔒 Demo credentials only — change them (and the seed data) before any real deployment.

---

## 🗺️ ArcGIS Integration

The platform uses **ArcGIS Online hosted feature layers** for all map data:

- **Destination feature layer** — tourist destinations with geometry.
- **Branches layer** — sponsor branch locations (includes the branch `Category` inherited from its sponsor).
- **Tourists table + nationality layer** — tourist records and nationality aggregates used by the analytics dashboard.

**Sync to ArcGIS** (Admin dashboard) performs a true bi-directional sync:

- **Push** — compares the DB with the layer and then **adds new, updates changed, deletes stale** records in one pass (batched to respect ArcGIS request limits), then reports `+added / ~updated / -deleted` per layer.
- **Pull** — imports destination updates from ArcGIS back into the database.

### `arcgis-proxy.py` (machine-specific)

Some Windows machines have a broken TLS stack (Schannel) where .NET cannot complete HTTPS handshakes. For those, the repo includes a tiny local OpenSSL bridge proxy (`arcgis-proxy.py`):

```bash
python arcgis-proxy.py        # listens on http://127.0.0.1:8765
```

When `ArcGIS:UseProxy = "true"`, the app rewrites ArcGIS URLs through the proxy and starts it automatically at launch. On healthy machines, set `"UseProxy": "false"`.

---

## 🤖 AI Features

- **RAG-powered AI chat assistant** (`/AiChat`) — tourists can chat about destinations, itineraries and Egypt travel tips. The system first retrieves live destination data from the project database through the RAG pipeline, augments the LLM prompt with validated destination information, and then generates responses using Gemini or OpenAI (configurable).
- **Sponsor AI tools** — an agent-based service (`Services/AiTools`) can draft and create sponsor branches from natural-language descriptions.
- **Chat history** — conversations are persisted through `ChatHistoryService`.

---

## 📱 Mobile API (JWT)

A JWT-protected REST API for mobile clients lives under `Controllers/MobileControllers`:

| Endpoint | Purpose |
|---|---|
| `MobileAccount` | register / login / profile (JWT issuance) |
| `MobileDestination` | browse destinations |
| `MobileMission` | missions & progress |
| `MobileReward` | rewards & redemption |
| `MobileTrip` | trip plans |

Set `Jwt:Key` in configuration to enable it.

---

## 🌐 Localization

The UI is fully localized with `IStringLocalizer`:

- **English** — `Resources/SharedResource.en.resx`
- **Arabic** — `Resources/SharedResource.ar.resx` (with RTL layout switching)

Culture is selectable from the UI and persisted across requests.

---

## 🧩 Data Seeding

On first startup, `DbInitializer` seeds JSON data from `SeedData/`:

- Users (Admin / Sponsor / tourists) with roles
- 10 sponsors & 56 branches (real chain locations: EgyptAir, Cilantro, Abou El Sid, Emeco Travel, Nile cruises, hotels…)
- Destinations, menu items
- **126 utilities** in Egypt (police stations, fire stations, hospitals, pharmacies)
- **516 tourist accounts** across ~217 nationalities with registration dates spread through the year (for demo analytics)

Seeding only runs when the respective table is empty.

---

## 📄 License

**Proprietary — All Rights Reserved.** This project is not open source. No part
of it may be copied, reproduced, distributed, or published without the prior
written permission of the copyright holder. See `LICENSE`.

*Built as a graduation project — ITI, Egypt.*
