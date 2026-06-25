# GitHub Copilot Instructions — HealthGoalsTracker

## What We Are Building

HealthGoalsTracker is a .NET MAUI app (Android primary, Windows for dev) that helps users track seven daily health goals plus one weekly-only goal (Strength Training). Each goal is represented as a tappable card that turns green on completion and triggers a confetti celebration. Completing all daily goals triggers a bigger celebration. Goals reset every day at midnight.

The app also tracks body measurements (weight + body fat %) over time with a SkiaSharp dual-axis line chart.

Users sign in with Google (via Microsoft Entra External ID). Their data is stored locally (SQLite, offline-first) and synced to an Azure Cosmos DB backend through Azure Functions. This allows seamless cross-device access.

---

## Default Goals

### Daily Goals (14 pts possible per day)

| # | Goal | Emoji | Points |
|---|------|-------|--------|
| 1 | Slept at least 7 hours | 😴 | 3 |
| 2 | Ate less than 2200 Calories | 🍽️ | 3 |
| 3 | Ate at least 150g of Protein | 🥩 | 3 |
| 4 | Movement | 🏃 | 2 |
| 5 | Drank at least 70oz of water | 💧 | 1 |
| 6 | Meditated for at least 5 minutes | 🧘 | 1 |
| 7 | Fasted for at least 12 hours | ⏱️ | 1 |

### Weekly Goal (not part of daily score)

| # | Goal | Emoji | Notes |
|---|------|-------|-------|
| 8 | Strength Training | 💪 | `IsWeeklyOnly = true`; tap once on each training day; up to 3 sessions per week count toward the weekly score |

Goals are fully user-editable: rename, change points, delete, add new ones, or toggle `IsWeeklyOnly`. Each card has an inline edit/delete menu. An "Add Goal" button lives on the main page.

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
| Animations | Pure MAUI `AbsoluteLayout`+`BoxView` for confetti; SkiaSharp for measurement line chart |

---

## Architecture

```
HealthGoalsTracker/
├── Models/               # Plain data classes (Goal, DailyRecord, DailyGoalEntry, UserSettings, NotificationSchedule, BodyMeasurement)
├── ViewModels/           # MVVM ViewModels using CommunityToolkit.Mvvm
│   ├── MainViewModel.cs
│   ├── HistoryViewModel.cs
│   ├── MeasurementsViewModel.cs
│   └── NotificationsViewModel.cs
├── Views/                # XAML pages
│   ├── HistoryPage.xaml
│   ├── MeasurementsPage.xaml
│   └── NotificationsPage.xaml
├── Controls/             # Reusable XAML controls
│   ├── GoalCard.xaml     # Single goal box — red/green, emoji icon, weekly badge, tap-to-complete
│   └── ConfettiView.cs   # Pure MAUI particle confetti (no SkiaSharp)
├── Services/
│   ├── IGoalService.cs           # Interface for goal CRUD + daily/weekly state
│   ├── LocalGoalService.cs       # SQLite implementation (offline-first, always used)
│   ├── IMeasurementService.cs    # Interface for body measurement CRUD
│   ├── LocalMeasurementService.cs# SQLite implementation for body measurements
│   ├── CloudSyncService.cs       # Azure Functions API client — syncs when online
│   ├── AuthService.cs            # MSAL Google sign-in via Entra External ID
│   └── IHealthNotificationService.cs / NotificationScheduler.cs
├── Platforms/
│   └── Android/
└── infra/                # Azure Bicep IaC templates
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
    public string IconEmoji { get; set; }  // e.g. "😴" — shown on GoalCard
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }    // false once user has edited it
    public bool IsWeeklyOnly { get; set; } // true = counts toward weekly score only (e.g. Strength Training)
}

public class DailyRecord
{
    public string Id { get; set; }         // GUID
    public string UserId { get; set; }
    public string Date { get; set; }       // "yyyy-MM-dd"
    public int TotalPointsEarned { get; set; }    // sum of completed non-weekly goals only
    public int TotalPointsPossible { get; set; }  // sum of all non-weekly goal points (= 14 for defaults)
    public DateTime UpdatedAt { get; set; }
}

public class DailyGoalEntry
{
    public string Id { get; set; }
    public string DailyRecordId { get; set; }
    public string GoalId { get; set; }
    public string GoalName { get; set; }   // snapshot
    public string IconEmoji { get; set; }  // snapshot
    public int GoalPoints { get; set; }    // snapshot
    public bool IsWeeklyOnly { get; set; } // snapshot
    public bool IsCompleted { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BodyMeasurement
{
    public string Id { get; set; }              // GUID
    public string UserId { get; set; }
    public string Date { get; set; }            // "yyyy-MM-dd"
    public double? WeightLbs { get; set; }
    public double? BodyFatPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserSettings { /* unchanged */ }
public class NotificationSchedule { /* unchanged */ }
public enum NotificationType { /* unchanged */ }
```

---

## Scoring Formula

### Daily Score
`TotalPointsEarned / TotalPointsPossible` where both values only include **non-weekly** (`IsWeeklyOnly = false`) goals.  
Default maximum: **14 pts** (Sleep 3 + Calories 3 + Protein 3 + Movement 2 + Water 1 + Meditate 1 + Fast 1).

### Weekly Score
```
weeklyAvg       = sum(TotalPointsEarned for days with data this Mon–Sun) / count(days with data)
trainingBonus   = min(strength training sessions logged Mon–Sun, 3)
weeklyScore     = weeklyAvg + trainingBonus          // max = 17
weeklyPercent   = weeklyScore / 17 × 100%
```
- "Days with data" = days that have a `DailyRecord` row (user opened the app that day).
- Weeks that haven't ended yet divide only by the number of days elapsed with data.
- The weekly percentage is the canonical display format for weekly performance.

---

## Daily Goal Flow

1. On app launch, load today's `DailyRecord` from SQLite (keyed by `DateOnly.FromDateTime(DateTime.Today)`).
2. Render one `GoalCard` per `Goal` — green if completed, red/inactive otherwise.
3. On tap (daily goal): add/remove from completed set, update `TotalPointsEarned`, save locally, trigger confetti, then sync to cloud async.
4. On tap (weekly-only goal): toggle completed for today, does NOT change `TotalPointsEarned`/`TotalPointsPossible`, still syncs.
5. At midnight (detected on next app foreground), create a new `DailyRecord` for the new day.

---

## Goal Cards (UI)

Each `GoalCard` shows:
- **Emoji icon** (large, left side) + goal name
- Points value (hidden for `IsWeeklyOnly` goals)
- **"Weekly"** badge for `IsWeeklyOnly` goals
- Completion status (red = incomplete, green = complete)
- A context menu (⋯ icon) to: **Edit Name** | **Edit Points** | **Toggle Weekly-Only** | **Delete Goal**

The main page has an **"+ Add Goal"** button below the card grid.

## Main Page Header

Shows two score lines:
- **"Today: 8 / 14"** — daily points earned / possible (non-weekly goals only)
- **"This week: 74%"** — weekly score percentage (see Scoring Formula)

---

## Celebration Animations

- **Single goal completed**: confetti burst from the tapped card (SkiaSharp particle system, ~1.5 seconds).
- **All goals completed**: full-screen confetti explosion with a brief congratulatory message (3 seconds).

---

## Hamburger Menu (Shell Flyout)

Use MAUI Shell's built-in flyout. Menu items:
- **History** → HistoryPage (calendar heatmap + daily breakdown + weekly score for selected week)
- **Measurements** → MeasurementsPage (weight + BF% entry form + SkiaSharp dual-axis line chart)
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

- **Calendar heatmap**: full-month calendar where each day cell is color-coded by daily completion percentage.
  - 0% = red, 50% = yellow, 100% = green, future = gray, no data = empty.
- **Tap a day** → expand a breakdown showing which goals were/weren't completed, points earned, and the **weekly score % for that week**.

## Body Measurements (MeasurementsPage)

- Entry form: date (defaults to today), weight in lbs (optional), body fat % (optional), optional notes.
- Does not need to be logged daily — just when the user weighs in.
- History display: **SkiaSharp dual-axis line chart** — weight on left Y-axis, BF% on right Y-axis, both plotted over time on the same chart.
- Backed by `BodyMeasurement` SQLite table via `LocalMeasurementService`.

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
| Historical data style | Calendar heatmap + per-day goal breakdown + weekly score for selected week |
| Target platforms | Android only (Windows included for dev convenience; no iOS/Mac) |
| Push notification implementation | Local device scheduling (no server push for simple schedules) |
| Daily reset time | Midnight local device time |
| Points system | Daily: non-weekly goals only, max 14 pts. Weekly: avg daily + training bonus, max 17, shown as % |
| Weekly-only goals | Any goal can be toggled `IsWeeklyOnly`; tapping logs one session; up to 3 strength sessions count |
| Weekly score divisor | Divide by days-with-data only (not always 7) so incomplete weeks score fairly |
| Goal card visuals | Each goal has an `IconEmoji`; weekly-only goals show a "Weekly" badge |
| Body measurement tracking | Dedicated Measurements page (hamburger); SkiaSharp dual-axis chart; no daily requirement |
| Chart library | Raw SkiaSharp (already a dependency via confetti) — no new NuGet packages |
