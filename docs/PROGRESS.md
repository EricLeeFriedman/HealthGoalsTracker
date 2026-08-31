# HealthGoalsTracker — Build Progress

This file lives in the repo so any agent or developer on any machine can pick up where work left off.

---

## Completed Phases

| Phase | Summary | Commit |
|-------|---------|--------|
| 1 | **Data layer** — Models (`Goal`, `DailyRecord`, `DailyGoalEntry`, `UserSettings`, `NotificationSchedule`), `IGoalService`, `LocalGoalService` (SQLite, lazy init with `SemaphoreSlim`, soft delete, daily entry snapshots) | `67c9e28` |
| 2 | **Main page UI** — `GoalCardViewModel`, `MainViewModel`, `GoalCard` ContentView (red/green), `MainPage` (CollectionView + score header), Shell + DI wiring | `d0485ed` |
| 3 | **Goal editing correctness** — `UpdateTodayGoalSnapshotAsync` / `RemoveTodayGoalEntryAsync`, input validation, removed iOS/Mac platform folders | `36dcdb6` |
| 4 | **Confetti & celebrations** — `ConfettiView` (pure MAUI `AbsoluteLayout`+`BoxView`, no SkiaSharp), `CelebrationBanner` spring-in animation, `CelebrationMessage` via `WeakReferenceMessenger`, card tap press animation | `52c8801` |
| 5 | **Hamburger menu (Shell flyout)** — `FlyoutBehavior=Flyout`, branded purple header, nav items (Home / History / Notifications), action items (Reset Today, Export Data as JSON share, About), `ResetTodayAsync()` added to service | `1f615ad` |
| 6 | **History page (calendar heatmap)** — `CalendarDayViewModel` (color-coded by %, padding cells), `HistoryViewModel` (month nav, day selection, goal breakdown panel), `HistoryPage.xaml` 7-col CollectionView grid, color legend, tap-to-expand day detail | `71fa520` |
| 7 | **Push notifications** — `Plugin.LocalNotification` 11.1.4, `IHealthNotificationService` / `NotificationScheduler`, 4 daily alarms (2 nudges + summary + recap), nudges cancelled on first goal completion, full `NotificationsPage` with master toggle + per-notification time pickers | `ccffc4f` |
| 8 | **Emoji goal cards + updated defaults** — `IconEmoji` + `IsWeeklyOnly` added to models; new 7+1 default goals with emojis and correct points; schema migration via `ALTER TABLE ADD COLUMN` + `UPDATE` patches for existing rows; `GoalCard` tile shows large emoji; Edit Emoji option; db bumped to `healthgoals_v2.db3` for clean reseed | `19f4750` + `cb53b80` |
| 8b | **Confetti animation overhaul** — `ConfettiView` rewritten as `GraphicsView`+`IDrawable` (eliminates first-frame hitch); burst explosion from tapped card center using projectile arc physics; rain effect for all-goals-complete; concurrent burst support; 80% fall speed; canvas-relative Y coordinate fix; CRLF enforced | `3d1107e` + `5b90886` |
| 9+10 | **Weekly scoring + main page header** — `GetWeeklyScoreAsync` (avg daily pts over days-with-data + min(training,3), max 17, as %); daily score correctly excludes `IsWeeklyOnly` goals; `GoalCard` shows `🗓 Weekly` badge; "Toggle Weekly-Only" in options menu; `AddGoalAsync` prompts for weekly-only; header shows `Today: X / 14` + `This week: 74%` | `698a3e2` |
| 11 | **Body measurements** — `BodyMeasurement`, SQLite-backed `IMeasurementService` / `LocalMeasurementService`, one measurement per user/date with update-on-resave behavior, entry form, recent-history list, Shell navigation, DI wiring, and a MAUI `GraphicsView` dual-axis chart supporting sparse weight/body-fat series | `abad07b` + `a809405` |
| Build cleanup | Replaced obsolete MAUI `Frame` usage, migrated CommunityToolkit observable fields to WinRT/AOT-compatible partial properties, and pinned patched SQLite native binaries for warning-free Android and Windows builds | `abad07b` |
| 10 follow-up | **History weekly score** — selected-day breakdown shows the canonical Monday–Sunday weekly percentage and date range, including weeks where the selected day has no record | `269bc7a` |
| Test foundation | **Automated business-logic coverage** — Windows-targeted xUnit project covers measurement upsert/order behavior, weekly averaging/session caps/user isolation, Monday week boundaries, and chart range handling | `b15c63d` |
| Diagnostics | **Persistent runtime diagnostics** — bounded rotating file logs for lifecycle, page-load, persistence, notification, export, and unhandled-error events; excludes health values and identifiers; Shell action exports a stable log snapshot | `2ebcf5b` |
| Runtime verification | **Windows + Android smoke verification** — isolated Windows UI Automation through Home/Measurements/History with synthetic persistence and screenshots; Android API 36 deployment with screenshot, UI hierarchy, app diagnostics, notification scheduling, and logcat inspection | `7ba9ddc` + `eb1ff42` |
| Cloud contract | **Authentication and synchronization design** — token trust boundary, versioned API routes, sync envelopes, idempotency, conflict/retry behavior, Cosmos layout, and diagnostic privacy rules documented before implementation | `e46858e` |

---

## Remaining Phases

### Phase 12 — Authentication ⛔ NEEDS USER INTERVENTION
**What to do before starting:**
1. Create a **Microsoft Entra External ID tenant** (free — 50K MAU/month) at https://entra.microsoft.com
2. Register the app → Platform: Mobile/Desktop → Redirect URI: `msal{ClientId}://auth`
3. Create **Google OAuth credentials** in Google Cloud Console and add Google as a social IDP in the Entra tenant
4. Provide the **Client ID** and **Tenant ID** (or the authority URL `https://{tenant}.ciamlogin.com/{tenant}.onmicrosoft.com`) to the next agent session

**What the agent will build:**
- Add `Microsoft.Identity.Client` (MSAL) NuGet package
- `IAuthService` + `MsalAuthService` with `SignInAsync` / `SignOutAsync` / `GetTokenAsync`
- Google sign-in via Entra External ID as social IDP (no Xamarin.Essentials needed)
- Hamburger menu: Log In / Log Out items (conditional on auth state)
- `LocalGoalService.UpdateUserIdAsync()` migration hook called after first sign-in to re-stamp local data with the real user ID
- Follow the trust boundary and authentication outcomes in `docs/CLOUD-CONTRACTS.md`

### Phase 13 — Azure Functions Backend ⛔ NEEDS AZURE SUBSCRIPTION
- Azure Functions (Consumption) + Cosmos DB (free tier, 1000 RU/s)
- Versioned endpoints: `POST /api/v1/sync`, `GET /api/v1/records`, `GET /api/v1/goals`, `GET /api/v1/measurements`
- JWT validation via Entra External ID issuer
- Contract tests for validation, authenticated partitioning, and idempotent replay

### Phase 14 — Cloud Sync (depends on 12 + 13)
- `CloudSyncService`: fire-and-forget HTTP POST to Azure Functions after every local write
- Conflict resolution: last-write-wins by `UpdatedAt`
- Offline queue: retry on next app launch if request fails
- Sync body measurements along with goals and daily records
- Follow cursor, retry, conflict, and privacy rules in `docs/CLOUD-CONTRACTS.md`

### Phase 15 — Bicep IaC ⛔ NEEDS AZURE
- `/infra/main.bicep` provisioning all Azure resources (Functions, Cosmos DB, Entra app registration)
- `az deployment` command to stand everything up in one shot

### Phase 16 — CI/CD ⛔ NEEDS AZURE CREDENTIALS
- GitHub Actions: build + lint on PR, APK artifact on push to main
- Secrets: `AZURE_CREDENTIALS`, Entra `CLIENT_ID`, `TENANT_ID`

---

## Architecture & Key Conventions

| Topic | Detail |
|-------|--------|
| Platform | .NET MAUI, Android + Windows (dev only), .NET 10 |
| Local data | SQLite via `sqlite-net-pcl`; `LocalGoalService` for goals/history and `LocalMeasurementService` for measurements |
| MVVM | `CommunityToolkit.Mvvm`; observable state uses AOT-compatible partial properties; all members **public** except framework overrides that require narrower access |
| Messaging | `WeakReferenceMessenger` for VM-to-View |
| DI chain | `App` ← `AppShell` ← pages ← ViewModels ← local service interfaces |
| Auth (future) | MSAL + Microsoft Entra External ID with Google social IDP |
| Cloud (future) | Azure Functions (Consumption) + Cosmos DB (free tier) |
| `UserId` | `"local"` placeholder throughout; `UpdateUserIdAsync()` migrates on first sign-in |
| Soft delete | `Goal.IsDeleted` + `DeletedAt` for future cloud sync tombstones |
| Scoring | Daily: non-weekly goals only, max 14 pts. Weekly: avg(daily pts / days-with-data) + min(training,3), max 17, displayed as % |
| Notifications | `Plugin.LocalNotification` 11.1.4, interface named `IHealthNotificationService` to avoid conflict with the plugin's own `INotificationService` |

---

## Key File Locations

| File | Purpose |
|------|---------|
| `Services/IGoalService.cs` | Full data contract (includes `GetWeeklyScoreAsync`) |
| `Services/LocalGoalService.cs` | SQLite implementation |
| `Services/IMeasurementService.cs` | Body measurement data contract |
| `Services/LocalMeasurementService.cs` | SQLite implementation for body measurements |
| `Services/INotificationService.cs` | `IHealthNotificationService` interface |
| `Services/NotificationScheduler.cs` | Plugin.LocalNotification scheduling |
| `Services/DiagnosticsService.cs` | Rotating persistent logger and exportable diagnostic snapshots |
| `ViewModels/MainViewModel.cs` | Main page logic (toggle, add, edit, delete, daily + weekly score) |
| `ViewModels/HistoryViewModel.cs` | Calendar heatmap logic (includes weekly score for selected week) |
| `ViewModels/MeasurementsViewModel.cs` | Body measurement entry and recent-history state |
| `ViewModels/NotificationsViewModel.cs` | Notification settings logic |
| `Controls/ConfettiView.cs` | Pure MAUI particle confetti |
| `Controls/MeasurementChartView.cs` | Dual-axis `GraphicsView` chart for weight and body-fat history |
| `Controls/GoalCard.xaml` | Goal card UI (emoji, red/green, weekly badge, press animation) |
| `Views/HistoryPage.xaml` | Calendar heatmap page |
| `Views/MeasurementsPage.xaml` | Body measurements entry form, dual-axis chart, and recent history |
| `Views/NotificationsPage.xaml` | Notification settings page |
| `HealthGoalsTracker.Tests/` | xUnit coverage for SQLite services, scoring, week boundaries, and chart calculations |
| `scripts/verify-windows.ps1` | Isolated Windows UI automation, screenshots, and diagnostic assertions |
| `AppShell.xaml.cs` | Flyout nav + menu actions |
| `MauiProgram.cs` | All DI registrations |
| `.github/copilot-instructions.md` | Copilot context (goals, models, conventions) |
| `docs/ARCHITECTURE.md` | Architecture diagrams, data sync strategy |
| `docs/CLOUD-CONTRACTS.md` | Planned authentication, API, synchronization, and privacy contract |
| `docs/PROGRESS.md` | **This file** — phase tracker for agents |
