using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HealthGoalsTracker.Messages;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IGoalService GoalService;

    [ObservableProperty]
    ObservableCollection<GoalCardViewModel> goals = [];

    [ObservableProperty]
    string scoreText = "—";

    [ObservableProperty]
    string todayDateText = string.Empty;

    [ObservableProperty]
    bool allGoalsCompleted;

    [ObservableProperty]
    bool isLoading;

    public MainViewModel(IGoalService goalService)
    {
        GoalService = goalService;
        TodayDateText = DateTime.Today.ToString("dddd, MMMM d, yyyy");
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var record = await GoalService.GetTodayRecordAsync();
            var entries = await GoalService.GetDailyEntriesAsync(record.Id);
            var entryMap = entries.ToDictionary(e => e.GoalId);
            var goalList = await GoalService.GetGoalsAsync();

            Goals.Clear();
            foreach (var goal in goalList)
            {
                entryMap.TryGetValue(goal.Id, out var entry);
                Goals.Add(new GoalCardViewModel
                {
                    Goal = goal,
                    Entry = entry ?? new DailyGoalEntry { GoalId = goal.Id, GoalName = goal.Name, GoalPoints = goal.Points },
                    Name = goal.Name,
                    Points = goal.Points,
                    IsCompleted = entry?.IsCompleted ?? false,
                    OnToggleRequested = ToggleGoalInternalAsync,
                    OnOptionsRequested = ShowGoalOptionsInternalAsync
                });
            }

            UpdateScore(record);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // -------------------------------------------------------------------------
    // Toggle
    // -------------------------------------------------------------------------

    async Task ToggleGoalInternalAsync(GoalCardViewModel card)
    {
        bool wasCompleted = card.IsCompleted;
        await GoalService.ToggleGoalCompletionAsync(card.Goal.Id);
        card.IsCompleted = !card.IsCompleted;

        var record = await GoalService.GetTodayRecordAsync();
        UpdateScore(record);

        // Only celebrate when a goal is newly checked — not when unchecking.
        if (card.IsCompleted)
            WeakReferenceMessenger.Default.Send(new CelebrationMessage(AllGoalsCompleted));
    }

    // -------------------------------------------------------------------------
    // Goal options (edit name / edit points / delete)
    // -------------------------------------------------------------------------

    async Task ShowGoalOptionsInternalAsync(GoalCardViewModel card)
    {
        var page = GetCurrentPage();
        var action = await page.DisplayActionSheetAsync(card.Name, "Cancel", null,
            "Edit Name", "Edit Points", "Delete");

        switch (action)
        {
            case "Edit Name":   await EditGoalNameAsync(card, page);   break;
            case "Edit Points": await EditGoalPointsAsync(card, page); break;
            case "Delete":      await DeleteGoalAsync(card, page);     break;
        }
    }

    async Task EditGoalNameAsync(GoalCardViewModel card, Page page)
    {
        var newName = await page.DisplayPromptAsync(
            "Edit Goal Name", null,
            initialValue: card.Name,
            maxLength: 60,
            keyboard: Keyboard.Text);

        if (newName == null) return; // user cancelled

        newName = newName.Trim();
        if (newName.Length == 0)
        {
            await page.DisplayAlertAsync("Invalid Name", "Goal name cannot be empty.", "OK");
            return;
        }
        if (newName == card.Name) return;

        card.Goal.Name = newName;
        card.Name = newName;
        card.Goal.IsDefault = false;
        await GoalService.SaveGoalAsync(card.Goal);
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, newName, card.Goal.Points);
    }

    async Task EditGoalPointsAsync(GoalCardViewModel card, Page page)
    {
        var input = await page.DisplayPromptAsync(
            "Edit Points", $"Points for \"{card.Name}\" (1–99)",
            initialValue: card.Points.ToString(),
            maxLength: 2,
            keyboard: Keyboard.Numeric);

        if (input == null) return; // user cancelled

        if (!int.TryParse(input, out var newPoints) || newPoints < 1 || newPoints > 99)
        {
            await page.DisplayAlertAsync("Invalid Points", "Please enter a whole number between 1 and 99.", "OK");
            return;
        }
        if (newPoints == card.Points) return;

        card.Goal.Points = newPoints;
        card.Points = newPoints;
        card.Goal.IsDefault = false;
        await GoalService.SaveGoalAsync(card.Goal);
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, card.Goal.Name, newPoints);

        var record = await GoalService.GetTodayRecordAsync();
        UpdateScore(record);
    }

    async Task DeleteGoalAsync(GoalCardViewModel card, Page page)
    {
        var confirmed = await page.DisplayAlertAsync(
            "Delete Goal",
            $"Delete \"{card.Name}\"? This cannot be undone.",
            "Delete", "Cancel");

        if (!confirmed) return;

        await GoalService.DeleteGoalAsync(card.Goal.Id);
        await GoalService.RemoveTodayGoalEntryAsync(card.Goal.Id);
        Goals.Remove(card);

        var record = await GoalService.GetTodayRecordAsync();
        UpdateScore(record);
    }

    // -------------------------------------------------------------------------
    // Add goal
    // -------------------------------------------------------------------------

    [RelayCommand]
    async Task AddGoalAsync()
    {
        var page = GetCurrentPage();

        var name = await page.DisplayPromptAsync(
            "New Goal", "Enter a name for the goal",
            maxLength: 60,
            keyboard: Keyboard.Text);

        if (name == null) return; // user cancelled

        name = name.Trim();
        if (name.Length == 0)
        {
            await page.DisplayAlertAsync("Invalid Name", "Goal name cannot be empty.", "OK");
            return;
        }

        var pointsInput = await page.DisplayPromptAsync(
            "New Goal", $"Points for \"{name}\" (1–99)",
            initialValue: "1",
            maxLength: 2,
            keyboard: Keyboard.Numeric);

        if (pointsInput == null) return; // user cancelled

        if (!int.TryParse(pointsInput, out var pts) || pts < 1 || pts > 99)
        {
            await page.DisplayAlertAsync("Invalid Points", "Please enter a whole number between 1 and 99.", "OK");
            return;
        }

        var newGoal = new Goal
        {
            Name = name,
            Points = pts,
            SortOrder = Goals.Count
        };

        await GoalService.SaveGoalAsync(newGoal);

        // Reload so GetTodayRecordAsync reconciles the new goal into today's entries.
        await LoadAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    void UpdateScore(DailyRecord record)
    {
        var pct = record.TotalPointsPossible == 0 ? 0
            : (int)Math.Round((double)record.TotalPointsEarned / record.TotalPointsPossible * 100);
        ScoreText = $"{record.TotalPointsEarned} / {record.TotalPointsPossible} pts  •  {pct}%";
        AllGoalsCompleted = Goals.Count > 0 && Goals.All(g => g.IsCompleted);
    }

    static Page GetCurrentPage() =>
        Application.Current!.Windows[0].Page
            ?? throw new InvalidOperationException("No active page found.");
}
