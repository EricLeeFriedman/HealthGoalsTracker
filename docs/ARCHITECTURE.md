# HealthGoalsTracker — Architecture

## Overview

HealthGoalsTracker is an offline-first .NET MAUI app for **Android** (+ Windows for dev). Goal and body-measurement data currently lives in local SQLite. Authentication, cloud sync, and the serverless Azure backend shown below are planned future phases.

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
│    CloudSyncService (planned) ◄─┼── Best-effort, async
│    AuthService (planned)      ◄─┼── Google via Entra External ID
│    NotificationScheduler      ◄─┼── Local platform scheduling
└────────────┬────────────────────┘
             │ HTTPS (when online + signed in)
             ▼
┌─────────────────────────────────┐
│      Azure Functions (API)      │
│                                 │
│  POST /api/v1/sync              │  ← Push/pull all synchronized data
│  GET  /api/v1/records           │  ← Fetch history for a date range
│  GET  /api/v1/goals             │  ← Fetch user's goal list
│  GET  /api/v1/measurements      │  ← Fetch body measurement history
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│      Azure Cosmos DB            │
│                                 │
│  Container: goals               │  PK: /userId
│  Container: dailyRecords        │  PK: /userId  SK: date
│  Container: measurements        │  PK: /userId
└─────────────────────────────────┘
             ▲
┌─────────────────────────────────┐
│  Microsoft Entra External ID    │
│  (Google social login)          │
│  Issues JWT → validated by      │
│  Functions via EasyAuth          │
└─────────────────────────────────┘
```

## Current Local Persistence

- `LocalGoalService` owns goals, daily records, goal-entry snapshots, settings, and notification schedules.
- `LocalMeasurementService` owns `BodyMeasurement` rows in the same SQLite database.
- Measurements are unique by `(UserId, Date)`. Saving the same date again updates the existing row.
- `MeasurementsPage` provides date, optional weight, optional body-fat percentage, notes, recent history, and a `GraphicsView` chart.
- `MeasurementChartView` plots weight against the left Y-axis and body-fat percentage against the right Y-axis. It supports missing values in either series and spaces points by measurement date.
- Selecting a day in History shows both that day's completion and the canonical Monday–Sunday weekly score.

## Planned Data Sync Strategy

1. **App launch**: load today's `DailyRecord` from SQLite. If user is signed in and online, fetch any records modified on other devices since last sync.
2. **Goal tap**: write to SQLite immediately (UI updates instantly). Fire-and-forget HTTP POST to sync endpoint.
3. **Goal edits**: same pattern — local-first, async cloud sync.
4. **Conflict resolution**: last-write-wins, keyed by `UpdatedAt` timestamp.

The authoritative authentication, API payload, retry, privacy, and Cosmos partitioning
rules are defined in [`CLOUD-CONTRACTS.md`](CLOUD-CONTRACTS.md).

## Push Notifications

Notifications are **locally scheduled** on the device — no server push required for these
simple time-based patterns. Repeating alarms are refreshed on app launch and whenever
notification settings or goal completion state changes. Nudge schedules are omitted after
any goal is completed that day and restored if today's completions are reset.
Android notification permission is checked and requested before schedules are created;
denial is recorded without falsely reporting successful scheduling.

Azure Notification Hubs is reserved for future server-initiated pushes (e.g., streaks, achievements).
The unpackaged Windows target is for development and does not register local notifications;
scheduling is enabled on the primary Android target.

## Runtime Diagnostics

- `DiagnosticsService` writes structured UTC log entries to
  `FileSystem.AppDataDirectory/diagnostics/healthgoals.log`.
- Logs rotate at 2 MB and retain four archives.
- Application startup, page loads, persistence operations, exports, notification scheduling,
  and unhandled exceptions are recorded.
- Health values, goal names, notes, user identifiers, and database paths are intentionally
  excluded from diagnostic messages.
- The Shell's **Export Diagnostics** action creates a stable snapshot and opens the platform
  share sheet for inspection after a run.
- DEBUG builds accept `HEALTHGOALSTRACKER_DATA_DIR` to isolate runtime verification from
  real application data. `scripts/verify-windows.ps1` uses this override with Windows UI
  Automation. `scripts/verify-android.ps1` builds a self-contained APK and uses ADB plus
  UIAutomator against a booted emulator or device. Both runners consume the feature
  requirements in `scripts/live-tests/features.json`, write a machine-readable result
  report, and collect ignored screenshots and logs beneath `artifacts`.
- Live runs check flyout completeness and readability, calendar labels and seven-column
  geometry, goal completion/reset, measurement persistence, notification configuration,
  diagnostic privacy, and fatal process errors. Platform-specific drivers implement
  native interaction details without redefining expected product behavior.

## Visual Theme

The current UI uses fixed light surfaces, so the application explicitly requests the
light theme on every platform. Calendar day text selects a contrasting foreground for
light no-data, future, and amber cells. This prevents Windows system dark mode from
combining dark-theme foreground defaults with the app's white surfaces.

## Planned Azure Resource Group Layout

```
rg-healthgoalstracker-prod
├── Microsoft Entra External ID tenant (directory-level)
├── func-healthgoalstracker          (Azure Functions, Consumption)
├── cosmos-healthgoalstracker        (Cosmos DB, free tier)
├── ntfhub-healthgoalstracker        (Notification Hubs, free tier)
└── st<uniqueid>hgt                  (Storage Account for Functions host)
```

These resources and `/infra/main.bicep` are not implemented yet.

## Planned Authentication Flow

```
User taps "Sign in with Google"
  → MSAL.NET opens browser/WebView
  → Entra External ID handles Google OAuth
  → Returns JWT access token
  → Token stored in MSAL cache (Secure Storage on device)
  → Attached as Bearer header on all API calls
  → Functions validates via built-in EasyAuth
  → UserId = token subject claim (stable within the Entra tenant and API)
```

## Project Structure

```
/HealthGoalsTracker          ← MAUI app
  /Models                    ← Goal, DailyRecord, DailyGoalEntry, BodyMeasurement, UserSettings
  /ViewModels
  /Views                     ← HistoryPage, MeasurementsPage, NotificationsPage
  /Controls                  ← GoalCard, ConfettiView, MeasurementChartView
  /Services                  ← Local persistence, notifications, and bounded file diagnostics
  /Platforms/Android
  /infra                     ← Planned Bicep IaC

/HealthGoalsTracker.Tests    ← Requirement-driven feature, service, and presentation tests
/scripts
  /live-tests                ← Shared feature catalog and result helpers
  verify-windows.ps1         ← Isolated Windows UI verification
  verify-android.ps1         ← ADB/UIAutomator Android verification
/docs
  ARCHITECTURE.md
  CLOUD-CONTRACTS.md
  PROGRESS.md

/HealthGoalsTracker.Functions  ← Planned Azure Functions backend (C#, isolated worker)
  GoalsApi.cs
  SyncApi.cs
```

## Build & Deploy

- **Local dev**: `dotnet build -f net10.0-android` (or `net10.0-windows10.0.19041.0` for desktop testing)
- **Current full build**: `dotnet build` builds Android and Windows on Windows.
- **Automated tests**: `dotnet test HealthGoalsTracker.Tests\HealthGoalsTracker.Tests.csproj`
  covers goal lifecycle and scoring, history presentation, measurements, notification
  settings, exports, diagnostics, and UI-facing model state.
- **Windows live test**: `.\scripts\verify-windows.ps1` exercises Home goal completion
  and reset, the complete Shell flyout, Measurements, the History calendar and detail,
  and Notifications. It verifies visible content, flyout screenshot contrast, calendar
  geometry and date contrast, persisted state, and page-load diagnostics. The script
  writes ignored screenshots and diagnostics beneath `artifacts/windows-verification`.
- **Android live test**: `.\scripts\verify-android.ps1` builds and installs a standalone
  APK, resets only the app's test data, and exercises the same shared feature contract
  with UIAutomator selectors. It also checks notification permission, four scheduled
  alarms, app diagnostics, and app-scoped logcat errors.
- **Continuous integration**: `.github/workflows/ci.yml` runs on pushes and pull requests
  to `master`. A Windows runner restores once, runs the requirement tests, builds both
  target frameworks with warnings treated as errors, and retains TRX results and a
  self-contained signed development APK for 14 days.
- **Azure deployment**: planned; no deployment workflow or Bicep deployment is currently present.
