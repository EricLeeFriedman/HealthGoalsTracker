# HealthGoalsTracker — Architecture

## Overview

HealthGoalsTracker is an offline-first .NET MAUI app for **Android** (+ Windows for dev). Data lives in local SQLite and optionally syncs to a serverless Azure backend when the user is signed in and online.

```
┌─────────────────────────────────┐
│         MAUI App (Mobile)       │
│                                 │
│  MainPage (GoalCards)           │
│  HistoryPage (Calendar Heatmap) │
│  MeasurementsPage (Weight/BF%)  │
│  NotificationsPage              │
│                                 │
│  ViewModels (CommunityToolkit)  │
│  Services:                      │
│    LocalGoalService (SQLite)  ◄─┼── Always active (offline-first)
│    LocalMeasurementService    ◄─┼── Body measurements (SQLite)
│    CloudSyncService           ◄─┼── Best-effort, async
│    AuthService (MSAL)         ◄─┼── Google via Entra External ID
│    NotificationScheduler      ◄─┼── Local platform scheduling
└────────────┬────────────────────┘
             │ HTTPS (when online + signed in)
             ▼
┌─────────────────────────────────┐
│      Azure Functions (API)      │
│                                 │
│  POST /api/sync                 │  ← Upsert goals + daily records
│  GET  /api/records?from=&to=    │  ← Fetch history for a date range
│  GET  /api/goals                │  ← Fetch user's goal list
│  POST /api/goals                │  ← Save goal list
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│      Azure Cosmos DB            │
│                                 │
│  Container: goals               │  PK: /userId
│  Container: dailyRecords        │  PK: /userId  SK: date
└─────────────────────────────────┘
             ▲
┌─────────────────────────────────┐
│  Microsoft Entra External ID    │
│  (Google social login)          │
│  Issues JWT → validated by      │
│  Functions via EasyAuth          │
└─────────────────────────────────┘
```

## Data Sync Strategy

1. **App launch**: load today's `DailyRecord` from SQLite. If user is signed in and online, fetch any records modified on other devices since last sync.
2. **Goal tap**: write to SQLite immediately (UI updates instantly). Fire-and-forget HTTP POST to sync endpoint.
3. **Goal edits**: same pattern — local-first, async cloud sync.
4. **Conflict resolution**: last-write-wins, keyed by `UpdatedAt` timestamp.

## Push Notifications

Notifications are **locally scheduled** on the device — no server push required for these simple time-based patterns. The device reschedules each night based on user settings stored in SQLite.

Azure Notification Hubs is reserved for future server-initiated pushes (e.g., streaks, achievements).

## Azure Resource Group Layout

```
rg-healthgoalstracker-prod
├── Microsoft Entra External ID tenant (directory-level)
├── func-healthgoalstracker          (Azure Functions, Consumption)
├── cosmos-healthgoalstracker        (Cosmos DB, free tier)
├── ntfhub-healthgoalstracker        (Notification Hubs, free tier)
└── st<uniqueid>hgt                  (Storage Account for Functions host)
```

All resources are deployed via `/infra/main.bicep`.

## Authentication Flow

```
User taps "Sign in with Google"
  → MSAL.NET opens browser/WebView
  → Entra External ID handles Google OAuth
  → Returns JWT access token
  → Token stored in MSAL cache (Secure Storage on device)
  → Attached as Bearer header on all API calls
  → Functions validates via built-in EasyAuth
  → UserId = token subject claim (stable per Google account)
```

## Project Structure

```
/HealthGoalsTracker          ← MAUI app
  /Models                    ← Goal, DailyRecord, DailyGoalEntry, BodyMeasurement, UserSettings
  /ViewModels
  /Views                     ← HistoryPage, MeasurementsPage, NotificationsPage
  /Controls                  ← GoalCard (emoji + weekly badge), ConfettiView
  /Services                  ← IGoalService, LocalGoalService, IMeasurementService, LocalMeasurementService
  /Platforms/Android
  /infra                     ← Bicep IaC

/HealthGoalsTracker.Functions  ← Azure Functions backend (C#, isolated worker)
  GoalsApi.cs
  SyncApi.cs
```

## Build & Deploy

- **Local dev**: `dotnet build -f net10.0-android` (or `net10.0-windows10.0.19041.0` for desktop testing)
- **CI/CD**: GitHub Actions — build + test on PR; deploy Functions + Bicep on push to `main`
- **Azure setup**: run `az deployment group create --template-file infra/main.bicep` once to provision all resources
