using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Controls;

public partial class GoalCard : ContentView
{
    public GoalCard()
    {
        InitializeComponent();
    }

    async void OnCardButtonClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not GoalCardViewModel vm) return;

        var canvas = (Shell.Current?.CurrentPage as MainPage)?.ConfettiView;
        var cardPosition = GetPositionRelativeTo(canvas);
        vm.TapOrigin = cardPosition == null
            ? new Point(Width / 2, Height / 2)
            : new Point(
                cardPosition.Value.X + Width / 2,
                cardPosition.Value.Y + Height / 2);

        await this.ScaleToAsync(0.94, 60, Easing.SinOut);
        await vm.ToggleCommand.ExecuteAsync(null);
        await this.ScaleToAsync(1.0, 150, Easing.SpringOut);
    }

    public Point? GetPositionRelativeTo(VisualElement? target)
    {
        if (target == null) return null;

        var cardPosition = GetPositionInWindow(this);
        var targetPosition = GetPositionInWindow(target);
        return new Point(
            cardPosition.X - targetPosition.X,
            cardPosition.Y - targetPosition.Y);
    }

    public static Point GetPositionInWindow(VisualElement element)
    {
        var x = element.X;
        var y = element.Y;
        Element? current = element.Parent;
        while (current is VisualElement visual)
        {
            x += visual.X;
            y += visual.Y;
            current = visual.Parent;
        }

        return new Point(x, y);
    }
}
