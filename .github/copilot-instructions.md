# GitHub Copilot Instructions — HealthGoalsTracker

## What We Are Building

HealthGoalsTracker is a .NET MAUI app (Android primary, Windows for dev) that helps users track six daily health goals. Each goal is represented as a tappable card that turns green on completion and triggers a confetti celebration. Completing all six triggers a bigger celebration. Goals reset every day at midnight.

Users sign in with Google (via Microsoft Entra External ID). Their data is stored locally (SQLite, offline-first) and synced to an Azure Cosmos DB backend through Azure Functions. This allows seamless cross-device access.

---

## The Six Default Health Goals

| # | Goal | Default Points |
|---|------|---------------|
| 1 | Slept at least 7 hours | 3 |
| 2 | Ate less than 2200 Calories | 3 |
| 3 | Fasted for at least 12 hours | 2 |
| 4 | Drank at least 70oz of water | 2 |
| 5 | Ate at least 150g of Protein | 2 |
| 6 | Meditated for at least 5 minutes | 1 |

Goals are fully user-editable: rename, change points, delete, or add new ones. Each box displays an inline edit/delete option. An "Add Goal" button lives on the main page.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| App framework | .NET MAUI targeting Android (+ Windows for local dev) |
| Local persistence | SQLite via `sqlite-net-pcl` |
| Authentication | Microsoft Entra External ID (Google social login) via MSAL.NET |
| API backend | Azure Functions (Consumption plan — free tier) |
| Database | Azure Cosmos DB (free tier: 25 GB, 1 000 RU/s) |
| Push notifications | Azure Notification Hubs (free tier: 500 devices, 1 M pushes/month) + FCM (Android) |
| IaC | Azure Bicep |
| CI/CD | GitHub Actions |
| MVVM | CommunityToolkit.Mvvm |
| Animations | SkiaSharp (confetti) or Lottie (via SkiaSharp.Extended.UI.Maui) |

---

## Architecture

```
HealthGoalsTracker/
├── Models/               # Plain data classes (Goal, DailyRecord, UserSettings, NotificationSchedule)
├── ViewModels/           # MVVM ViewModels using CommunityToolkit.Mvvm
│   ├── MainViewModel.cs
│   ├── HistoryViewModel.cs
│   └── SettingsViewModel.cs
├── Views/                # XAML pages
│   ├── MainPage.xaml
│   ├── HistoryPage.xaml
│   └── SettingsPage.xaml
├── Controls/             # Reusable XAML controls
│   ├── GoalCard.xaml     # Single goal box — red/green, tap-to-complete, confetti trigger
│   └── ConfettiView.xaml # SkiaSharp confetti overlay
├── Services/
│   ├── IGoalService.cs         # Interface for goal CRUD + daily state
│   ├── LocalGoalService.cs     # SQLite implementation (offline-first, always used)
│   ├── CloudSyncService.cs     # Azure Functions API client — syncs when online
│   ├── AuthService.cs          # MSAL Google sign-in via Entra External ID
│   └── NotificationService.cs  # Interface + platform implementations
├── Platforms/
│   └── Android/
│       └── NotificationService.cs   # FCM push notifications
└── infra/                # Azure Bicep IaC templates
    ├── main.bicep
    ├── cosmos.bicep
    ├── functions.bicep
    └── notification-hubs.bicep

Backend (separate repo or /backend folder):
└── HealthGoalsTracker.Functions/
    ├── GoalsApi.cs        # CRUD for goals and daily records
    ├── SyncApi.cs         # Batch sync endpoint
    └── NotificationApi.cs # Trigger push notifications
```

---

## Coding Conventions

- **C-like style**: all members and methods are `public`. Do not use `private`, `protected`, or `internal`. All access modifiers should be `public`.
- **MVVM**: all logic lives in ViewModels. Code-behind files (`.xaml.cs`) contain only minimal wiring.
- **Nullable enabled**: use `?` types and null-checks consistently.
- **Offline-first**: the SQLite local store is always the source of truth. Cloud sync is best-effort and non-blocking. The app must be fully functional without a network connection.
- **No external dependencies beyond what is listed** in the tech stack unless discussed first.
- **One goal = one `GoalCard` control.** Do not inline goal rendering in the page XAML.

---

## Key Models

```csharp
public class Goal
{
    public string Id { get; set; }         // GUID
    public string Name { get; set; }
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }    // false once user has edited it
}

public class DailyRecord
{
    public string Id { get; set; }         // GUID
    public string UserId { get; set; }
    public DateOnly Date { get; set; }
    public List<string> CompletedGoalIds { get; set; }  // Goal IDs completed that day
    public int TotalPointsEarned { get; set; }
    public int TotalPointsPossible { get; set; }
}

public class UserSettings
{
    public string UserId { get; set; }
    public List<NotificationSchedule> Notifications { get; set; }
    public bool NotificationsEnabled { get; set; }
}

public class NotificationSchedule
{
    public string Id { get; set; }
    public NotificationType Type { get; set; }
    public TimeOnly Time { get; set; }
    public bool IsEnabled { get; set; }
}

public enum NotificationType
{
    NudgeIfNoGoalsCompleted,   // Noon + 4pm defaults
    DailySummary,              // 9pm default
    MorningRecap               // 7am default (recap of yesterday)
}
```

---

## Daily Goal Flow

1. On app launch, load today's `DailyRecord` from SQLite (keyed by `DateOnly.FromDateTime(DateTime.Today)`).
2. Render one `GoalCard` per `Goal` — green if `Goal.Id` is in `DailyRecord.CompletedGoalIds`, red otherwise.
3. On tap: add/remove goal ID from `CompletedGoalIds`, update `TotalPointsEarned`, save locally, trigger confetti, then sync to cloud async.
4. At midnight (detected on next app foreground), create a new `DailyRecord` for the new day.

---

## Goal Cards (UI)

Each `GoalCard` shows:
- Goal name
- Points value
- Completion status (red = incomplete, green = complete)
- A long-press or context menu (⋯ icon) to: **Edit Name** | **Edit Points** | **Delete Goal**

The main page has an **"+ Add Goal"** button below the card grid.

---

## Celebration Animations

- **Single goal completed**: confetti burst from the tapped card (SkiaSharp particle system, ~1.5 seconds).
- **All goals completed**: full-screen confetti explosion with a brief congratulatory message (3 seconds).

---

## Hamburger Menu (Shell Flyout)

Use MAUI Shell's built-in flyout. Menu items:
- **History** → HistoryPage (calendar heatmap + daily breakdown)
- **Notifications** → Notification schedule editor (enable/disable, edit times per NotificationType)
- **Account** → Sign in / Sign out (Google via MSAL)
- **Reset Today** → Clears today's DailyRecord (with confirmation dialog)
- **Export Data** → Exports all DailyRecords as JSON (share sheet)
- **About** → App version + credits

---

## Push Notifications

Three notification types, all configurable per-device in the Notifications settings page:

| Type | Default Times | Description |
|------|--------------|-------------|
| `NudgeIfNoGoalsCompleted` | 12:00 PM, 4:00 PM | Sent only if zero goals completed that day |
| `DailySummary` | 9:00 PM | "You completed X/Y goals today (N points)" |
| `MorningRecap` | 7:00 AM | "Yesterday: X/Y goals, N points. Today is a new day!" |

Notifications are scheduled locally on the device using platform APIs (no server-side scheduling needed for these simple patterns).

---

## Historical Data View

- **Calendar heatmap**: full-month calendar where each day cell is color-coded by completion percentage.
  - 0% = red, 50% = yellow, 100% = green, future = gray, no data = empty.
- **Tap a day** → expand a breakdown of which goals were/weren't completed and the points earned.

---

## Azure Infrastructure (Free Tier)

All Azure resources must fit within the **always-free** or **Consumption/Serverless** free tiers:

| Resource | SKU / Tier |
|----------|-----------|
| Microsoft Entra External ID | Free (50 000 MAU/month) |
| Azure Functions | Consumption (1 M executions/month free) |
| Azure Cosmos DB | Free tier (25 GB, 1 000 RU/s) — one per subscription |
| Azure Notification Hubs | Free (500 active devices, 1 M pushes/month) |
| Azure Storage (Functions host) | LRS, minimal usage |

Deploy via Bicep (`/infra/main.bicep`). GitHub Actions deploy on push to `main`.

---

## Open Questions / Design Decisions Resolved

| Question | Answer |
|----------|--------|
| Cross-device login | Google sign-in via Microsoft Entra External ID (MSAL.NET) |
| Data backup | Offline-first SQLite; async sync to Cosmos DB via Azure Functions |
| Historical data style | Calendar heatmap + per-day goal breakdown |
| Target platforms | Android only (Windows included for dev convenience; no iOS/Mac) |
| Push notification implementation | Local device scheduling (no server push for simple schedules) |
| Daily reset time | Midnight local device time |
| Points system | Each goal has an editable point value; end-of-day score = earned/possible × 100% |
