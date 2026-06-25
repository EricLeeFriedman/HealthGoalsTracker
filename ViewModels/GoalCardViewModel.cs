using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.ViewModels;

public partial class GoalCardViewModel : ObservableObject
{
    public Goal Goal = null!;
    public DailyGoalEntry Entry = null!;

    // Callbacks wired by MainViewModel so the card VM stays decoupled from the service.
    public Func<GoalCardViewModel, Task> OnToggleRequested = _ => Task.CompletedTask;
    public Func<GoalCardViewModel, Task> OnOptionsRequested = _ => Task.CompletedTask;

    // Set by GoalCard.xaml.cs immediately before invoking ToggleCommand so
    // MainViewModel can pass the tap origin to the celebration confetti.
    public Point TapOrigin { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardColor))]
    [NotifyPropertyChangedFor(nameof(CompletionIcon))]
    bool isCompleted;

    [ObservableProperty]
    string name = string.Empty;

    [ObservableProperty]
    string iconEmoji = "⭐";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PointsBadgeText))]
    int points;

    public Color CardColor => IsCompleted
        ? Color.FromArgb("#43A047")   // green-600
        : Color.FromArgb("#E53935");  // red-600

    public string PointsBadgeText => Points == 1 ? "1 pt" : $"{Points} pts";

    public string CompletionIcon => IsCompleted ? "✓" : "";

    [RelayCommand]
    Task Toggle() => OnToggleRequested(this);

    [RelayCommand]
    Task OpenOptions() => OnOptionsRequested(this);
}
