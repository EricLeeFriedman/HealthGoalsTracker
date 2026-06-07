using System.Text.Json;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.Views;

namespace HealthGoalsTracker
{
    public partial class AppShell : Shell
    {
        public IGoalService GoalService;

        public AppShell(IServiceProvider services, IGoalService goalService)
        {
            InitializeComponent();
            GoalService = goalService;

            AddNavigationItems(services);
            AddActionItems();
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
                Title = "🔔  Notifications",
                Content = services.GetRequiredService<NotificationsPage>(),
                Route = "notifications"
            });
        }

        void AddActionItems()
        {
            var resetItem = new MenuItem { Text = "🔁  Reset Today" };
            resetItem.Clicked += OnResetTodayClicked;
            Items.Add(resetItem);

            var exportItem = new MenuItem { Text = "📤  Export Data" };
            exportItem.Clicked += OnExportDataClicked;
            Items.Add(exportItem);

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

            // Navigate home so MainPage reloads via OnAppearing.
            await GoToAsync("//home");
        }

        async void OnExportDataClicked(object? sender, EventArgs e)
        {
            try
            {
                var from = new DateOnly(2020, 1, 1);
                var to   = DateOnly.FromDateTime(DateTime.Today);
                var records = await GoalService.GetRecordsForRangeAsync(from, to);

                var days = new List<object>();
                foreach (var rec in records)
                {
                    var entries = await GoalService.GetDailyEntriesAsync(rec.Id);
                    days.Add(new
                    {
                        date            = rec.Date,
                        pointsEarned    = rec.TotalPointsEarned,
                        pointsPossible  = rec.TotalPointsPossible,
                        completionPct   = Math.Round(rec.CompletionPercent, 1),
                        goals           = entries.Select(entry => new
                        {
                            name      = entry.GoalName,
                            points    = entry.GoalPoints,
                            completed = entry.IsCompleted
                        })
                    });
                }

                var export = new { exportedAt = DateTime.UtcNow, days };
                var json = JsonSerializer.Serialize(export,
                    new JsonSerializerOptions { WriteIndented = true });

                var path = Path.Combine(FileSystem.CacheDirectory, "health_goals_export.json");
                await File.WriteAllTextAsync(path, json);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Health Goals Export",
                    File  = new ShareFile(path)
                });
            }
            catch (Exception ex)
            {
                var page = Current?.CurrentPage;
                if (page != null)
                    await page.DisplayAlertAsync("Export Failed", ex.Message, "OK");
            }
        }

        async void OnAboutClicked(object? sender, EventArgs e)
        {
            var page = Current?.CurrentPage;
            if (page == null) return;

            await page.DisplayAlertAsync(
                "Health Goals Tracker",
                "Track your six daily health goals.\n\nVersion 1.0\nBuilt with .NET MAUI",
                "OK");
        }
    }
}
