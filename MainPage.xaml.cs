using CommunityToolkit.Mvvm.Messaging;
using HealthGoalsTracker.Messages;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker
{
    public partial class MainPage : ContentPage
    {
        public MainViewModel ViewModel;

        // Exposed so GoalCard can resolve tap coordinates in the canvas draw-space.
        public Controls.ConfettiView ConfettiView => ConfettiCanvas;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Unregister first so re-entering the page doesn't double-register.
            WeakReferenceMessenger.Default.Unregister<CelebrationMessage>(this);
            WeakReferenceMessenger.Default.Register<CelebrationMessage>(this, (_, msg) =>
                MainThread.BeginInvokeOnMainThread(() => TriggerCelebration(msg.AllGoalsComplete, msg.CardTapOrigin)));

            await ViewModel.LoadAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            WeakReferenceMessenger.Default.Unregister<CelebrationMessage>(this);
        }

        async void TriggerCelebration(bool allGoalsComplete, Point tapOrigin)
        {
            if (allGoalsComplete)
            {
                // All goals done: full-screen confetti rain + banner.
                _ = ConfettiCanvas.PlayAllGoalsAsync();
                await ShowCelebrationBannerAsync();
            }
            else
            {
                // Single goal: explosion burst from the tapped card.
                _ = ConfettiCanvas.PlayBurstAsync(tapOrigin);
            }
        }

        async Task ShowCelebrationBannerAsync()
        {
            CelebrationBanner.Scale = 0.6;
            CelebrationBanner.Opacity = 0;
            CelebrationBanner.IsVisible = true;

            await Task.WhenAll(
                CelebrationBanner.ScaleToAsync(1.0, 350, Easing.SpringOut),
                CelebrationBanner.FadeToAsync(1.0, 250)
            );

            await Task.Delay(2500);

            await CelebrationBanner.FadeToAsync(0, 350, Easing.SinIn);
            CelebrationBanner.IsVisible = false;

            // Reset for next time.
            CelebrationBanner.Opacity = 1;
            CelebrationBanner.Scale = 1;
        }
    }
}
