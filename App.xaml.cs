using HealthGoalsTracker.Services;

namespace HealthGoalsTracker
{
    public partial class App : Application
    {
        public AppShell AppShellInstance;

        public App(AppShell appShell, IHealthNotificationService notificationService)
        {
            InitializeComponent();
            AppShellInstance = appShell;

            // Schedule (or re-schedule) notifications on every app launch.
            _ = notificationService.RescheduleAllAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(AppShellInstance);
        }
    }
}
