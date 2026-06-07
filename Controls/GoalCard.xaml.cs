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

        await this.ScaleToAsync(0.94, 60, Easing.SinOut);
        vm.ToggleCommand.Execute(null);
        await this.ScaleToAsync(1.0, 150, Easing.SpringOut);
    }
}
