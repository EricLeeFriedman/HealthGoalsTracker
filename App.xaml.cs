using HealthGoalsTracker.Services;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker
{
    public partial class App : Application
    {
        public AppShell AppShellInstance;
        public ILogger<App> Logger;

        public App(
            AppShell appShell,
            IHealthNotificationService notificationService,
            ILogger<App> logger)
        {
            InitializeComponent();
            AppShellInstance = appShell;
            Logger = logger;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Logger.LogInformation("Application started");
            _ = ScheduleNotificationsAsync(notificationService);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(AppShellInstance);
        }

        public async Task ScheduleNotificationsAsync(
            IHealthNotificationService notificationService)
        {
            if (OperatingSystem.IsWindows())
            {
                Logger.LogInformation(
                    "Notification scheduling skipped on the Windows development target");
                return;
            }

            try
            {
                await notificationService.RescheduleAllAsync();
                Logger.LogInformation("Notifications scheduled");
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Notification scheduling failed");
            }
        }

        public void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Logger.LogCritical(
                args.ExceptionObject as Exception,
                "Unhandled application exception; terminating={IsTerminating}",
                args.IsTerminating);
        }

        public void OnUnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs args)
        {
            Logger.LogError(args.Exception, "Unobserved task exception");
        }
    }
}
