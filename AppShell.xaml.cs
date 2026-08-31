using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;
using HealthGoalsTracker.Views;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker
{
    public partial class AppShell : Shell
    {
        public IGoalService GoalService;
        public MainViewModel MainViewModel;
        public IHealthNotificationService NotificationService;
        public IDiagnosticsService DiagnosticsService;
        public ILogger<AppShell> Logger;

        public AppShell(
            IServiceProvider services,
            IGoalService goalService,
            MainViewModel mainViewModel,
            IHealthNotificationService notificationService,
            IDiagnosticsService diagnosticsService,
            ILogger<AppShell> logger)
        {
            InitializeComponent();
            GoalService = goalService;
            MainViewModel = mainViewModel;
            NotificationService = notificationService;
            DiagnosticsService = diagnosticsService;
            Logger = logger;

            AddNavigationItems(services);
            AddActionItems();
            Logger.LogInformation("Application shell initialized");
        }

        void AddNavigationItems(IServiceProvider services)
        {
            Items.Add(new ShellContent
            {
                Title = "🏠  Home",
                Content = services.GetRequiredService<MainPage>(),
                Route = "home"
            });
            Items.Add(new ShellContent
            {
                Title = "📅  History",
                Content = services.GetRequiredService<HistoryPage>(),
                Route = "history"
            });
            Items.Add(new ShellContent
            {
                Title = "📊  Measurements",
                Content = services.GetRequiredService<MeasurementsPage>(),
                Route = "measurements"
            });
            Items.Add(new ShellContent
            {
                Title = "🔔  Notifications",
                Content = services.GetRequiredService<NotificationsPage>(),
                Route = "notifications"
            });
        }

        void AddActionItems()
        {
            var resetItem = new MenuItem
            {
                AutomationId = "ResetToday",
                Text = "🔁  Reset Today"
            };
            resetItem.Clicked += OnResetTodayClicked;
            Items.Add(resetItem);

            var exportItem = new MenuItem { Text = "📤  Export Data" };
            exportItem.Clicked += OnExportDataClicked;
            Items.Add(exportItem);

            var diagnosticsItem = new MenuItem { Text = "🩺  Export Diagnostics" };
            diagnosticsItem.Clicked += OnExportDiagnosticsClicked;
            Items.Add(diagnosticsItem);

            var aboutItem = new MenuItem { Text = "ℹ️  About" };
            aboutItem.Clicked += OnAboutClicked;
            Items.Add(aboutItem);
        }

        // -------------------------------------------------------------------------
        // Action handlers
        // -------------------------------------------------------------------------

        async void OnResetTodayClicked(object? sender, EventArgs e)
        {
            var page = Current?.CurrentPage;
            if (page == null) return;

            var confirmed = await page.DisplayAlertAsync(
                "Reset Today",
                "This will uncheck all goals for today. Your history for previous days is unaffected.",
                "Reset", "Cancel");

            if (!confirmed) return;

            await GoalService.ResetTodayAsync();
            await MainViewModel.LoadAsync();
            await NotificationService.RescheduleAllAsync();
            Logger.LogInformation("Reset Today completed");

            await GoToAsync("//home");
        }

        async void OnExportDataClicked(object? sender, EventArgs e)
        {
            try
            {
                var json = await DataExportService.BuildJsonAsync(GoalService);

                var path = Path.Combine(FileSystem.CacheDirectory, "health_goals_export.json");
                await File.WriteAllTextAsync(path, json);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Health Goals Export",
                    File  = new ShareFile(path)
                });
                Logger.LogInformation("Data export prepared");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Data export failed");
                var page = Current?.CurrentPage;
                if (page != null)
                    await page.DisplayAlertAsync("Export Failed", ex.Message, "OK");
            }
        }

        public async void OnExportDiagnosticsClicked(object? sender, EventArgs e)
        {
            try
            {
                Logger.LogInformation("Diagnostics export requested");
                var path = DiagnosticsService.CreateSnapshot(FileSystem.CacheDirectory);
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Health Goals Diagnostics",
                    File = new ShareFile(path)
                });
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Diagnostics export failed");
                var page = Current?.CurrentPage;
                if (page != null)
                    await page.DisplayAlertAsync(
                        "Diagnostics Export Failed",
                        exception.Message,
                        "OK");
            }
        }

        async void OnAboutClicked(object? sender, EventArgs e)
        {
            var page = Current?.CurrentPage;
            if (page == null) return;

            await page.DisplayAlertAsync(
                "Health Goals Tracker",
                "Track seven daily health goals and weekly strength training.\n\nVersion 1.0\nBuilt with .NET MAUI",
                "OK");
        }
    }
}
