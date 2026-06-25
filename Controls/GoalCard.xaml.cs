using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Controls;

public partial class GoalCard : ContentView
{
    public GoalCard()
    {
        InitializeComponent();
    }

    async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not GoalCardViewModel vm) return;

        // Get tap position in ConfettiCanvas draw-space (= ContentPage content coords,
        // below the Shell nav bar). Using the canvas as relativeTo avoids the Y offset
        // that appears when using null (window coords, which include the nav bar height).
        var canvas      = (Shell.Current?.CurrentPage as MainPage)?.ConfettiView;
        var tapInCanvas = e.GetPosition(canvas) ?? e.GetPosition(null) ?? Point.Zero;
        var tapInCard   = e.GetPosition(this)   ?? new Point(Width / 2, Height / 2);

        // card top-left in canvas coords = tapInCanvas - tapInCard
        // card centre in canvas coords   = top-left + (Width/2, Height/2)
        vm.TapOrigin = new Point(
            tapInCanvas.X - tapInCard.X + Width  / 2,
            tapInCanvas.Y - tapInCard.Y + Height / 2);

        await this.ScaleToAsync(0.94, 60, Easing.SinOut);
        vm.ToggleCommand.Execute(null);
        await this.ScaleToAsync(1.0, 150, Easing.SpringOut);
    }
}
