using Microsoft.Extensions.DependencyInjection;

namespace HealthGoalsTracker
{
    public partial class App : Application
    {
        public AppShell AppShellInstance;

        public App(AppShell appShell)
        {
            InitializeComponent();
            AppShellInstance = appShell;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(AppShellInstance);
        }
    }
}
