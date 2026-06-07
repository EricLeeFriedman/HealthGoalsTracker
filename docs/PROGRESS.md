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

---

## Remaining Phases

### Phase 8 — Authentication ⛔ NEEDS USER INTERVENTION
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

### Phase 9 — Azure Functions Backend ⛔ NEEDS AZURE SUBSCRIPTION
- Azure Functions (Consumption) + Cosmos DB (free tier, 1000 RU/s)
- Endpoints: `POST /sync`, `GET /records`, `GET /goals`, `POST /goals`
- JWT validation via Entra External ID issuer

### Phase 10 — Cloud Sync (depends on 8 + 9)
- `CloudSyncService`: fire-and-forget HTTP POST to Azure Functions after every local write
- Conflict resolution: last-write-wins by `UpdatedAt`
- Offline queue: retry on next app launch if request fails

### Phase 11 — Bicep IaC ⛔ NEEDS AZURE
- `/infra/main.bicep` provisioning all Azure resources (Functions, Cosmos DB, Entra app registration)
- `az deployment` command to stand everything up in one shot

### Phase 12 — CI/CD ⛔ NEEDS AZURE CREDENTIALS
- GitHub Actions: build + lint on PR, APK artifact on push to main
- Secrets: `AZURE_CREDENTIALS`, Entra `CLIENT_ID`, `TENANT_ID`

---

## Architecture & Key Conventions

| Topic | Detail |
|-------|--------|
| Platform | .NET MAUI, Android + Windows (dev only), .NET 10 |
| Local data | SQLite via `sqlite-net-pcl`, `LocalGoalService` |
| MVVM | `CommunityToolkit.Mvvm`, all members **public** (no private/protected — C-like style) |
| Messaging | `WeakReferenceMessenger` for VM-to-View |
| DI chain | `App` ← `AppShell` ← pages ← ViewModels ← `IGoalService` |
| Auth (future) | MSAL + Microsoft Entra External ID with Google social IDP |
| Cloud (future) | Azure Functions (Consumption) + Cosmos DB (free tier) |
| `UserId` | `"local"` placeholder throughout; `UpdateUserIdAsync()` migrates on first sign-in |
| Soft delete | `Goal.IsDeleted` + `DeletedAt` for future cloud sync tombstones |
| Build command | `dotnet build -f net10.0-android` (XA0141 SQLite warning is expected/harmless) |
| Notifications | `Plugin.LocalNotification` 11.1.4, interface named `IHealthNotificationService` to avoid conflict with the plugin's own `INotificationService` |

---

## Key File Locations

| File | Purpose |
|------|---------|
| `Services/IGoalService.cs` | Full data contract |
| `Services/LocalGoalService.cs` | SQLite implementation (~370 lines) |
| `Services/INotificationService.cs` | `IHealthNotificationService` interface |
| `Services/NotificationScheduler.cs` | Plugin.LocalNotification scheduling |
| `ViewModels/MainViewModel.cs` | Main page logic (toggle, add, edit, delete) |
| `ViewModels/HistoryViewModel.cs` | Calendar heatmap logic |
| `ViewModels/NotificationsViewModel.cs` | Notification settings logic |
| `Controls/ConfettiView.cs` | Pure MAUI particle confetti |
| `Controls/GoalCard.xaml` | Goal card UI (red/green, press animation) |
| `Views/HistoryPage.xaml` | Calendar heatmap page |
| `Views/NotificationsPage.xaml` | Notification settings page |
| `AppShell.xaml.cs` | Flyout nav + menu actions |
| `MauiProgram.cs` | All DI registrations |
| `.github/copilot-instructions.md` | Copilot context (goals, models, conventions) |
| `docs/ARCHITECTURE.md` | Architecture diagrams, data sync strategy |
| `docs/PROGRESS.md` | **This file** — phase tracker for agents |
