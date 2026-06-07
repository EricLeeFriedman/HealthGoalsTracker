using Microsoft.Extensions.Logging;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;
using HealthGoalsTracker.Views;

namespace HealthGoalsTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "healthgoals.db3");
            builder.Services.AddSingleton<IGoalService>(_ => new LocalGoalService(dbPath));
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<HistoryPage>();
            builder.Services.AddSingleton<NotificationsPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
