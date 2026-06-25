using Microsoft.Extensions.Logging;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;
using HealthGoalsTracker.Views;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace HealthGoalsTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification(config =>
                {
                    config.AddAndroid(android =>
                    {
                        android.AddChannel(new NotificationChannelRequest
                        {
                            Id          = "health_goals",
                            Name        = "Health Goals",
                            Description = "Daily health goal reminders",
                            Importance  = AndroidImportance.High
                        });
                    });
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "healthgoals_v2.db3");
            builder.Services.AddSingleton<IGoalService>(_ => new LocalGoalService(dbPath));
            builder.Services.AddSingleton<IHealthNotificationService, NotificationScheduler>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<HistoryViewModel>();
            builder.Services.AddSingleton<NotificationsViewModel>();
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
