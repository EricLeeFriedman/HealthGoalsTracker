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
    public IHealthNotificationService NotificationService;

    [ObservableProperty]
    ObservableCollection<GoalCardViewModel> goals = [];

    [ObservableProperty]
    string dailyScoreText = "—";

    [ObservableProperty]
    string weeklyScoreText = "—";

    [ObservableProperty]
    string todayDateText = string.Empty;

    [ObservableProperty]
    bool allGoalsCompleted;

    [ObservableProperty]
    bool isLoading;

    public MainViewModel(IGoalService goalService, IHealthNotificationService notificationService)
    {
        GoalService = goalService;
        NotificationService = notificationService;
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
                    Entry = entry ?? new DailyGoalEntry { GoalId = goal.Id, GoalName = goal.Name, IconEmoji = goal.IconEmoji, GoalPoints = goal.Points },
                    Name = goal.Name,
                    IconEmoji = goal.IconEmoji,
                    Points = goal.Points,
                    IsWeeklyOnly = goal.IsWeeklyOnly,
                    IsCompleted = entry?.IsCompleted ?? false,
                    OnToggleRequested = ToggleGoalInternalAsync,
                    OnOptionsRequested = ShowGoalOptionsInternalAsync
                });
            }

            await UpdateScoreAsync(record);
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
        await UpdateScoreAsync(record);

        // Only celebrate when a goal is newly checked — not when unchecking.
        if (card.IsCompleted)
        {
            WeakReferenceMessenger.Default.Send(new CelebrationMessage(AllGoalsCompleted, card.TapOrigin));

            // Cancel nudge notifications when the first goal of the day is completed.
            bool isFirstCompletion = Goals.Count(g => g.IsCompleted) == 1;
            if (isFirstCompletion)
                _ = NotificationService.CancelNudgesAsync();
        }
    }

    // -------------------------------------------------------------------------
    // Goal options (edit name / edit points / delete)
    // -------------------------------------------------------------------------

    async Task ShowGoalOptionsInternalAsync(GoalCardViewModel card)
    {
        var page = GetCurrentPage();
        var action = await page.DisplayActionSheetAsync(card.Name, "Cancel", null,
            "Edit Name", "Edit Points", "Edit Emoji", "Toggle Weekly-Only", "Delete");

        switch (action)
        {
            case "Edit Name":          await EditGoalNameAsync(card, page);        break;
            case "Edit Points":        await EditGoalPointsAsync(card, page);      break;
            case "Edit Emoji":         await EditGoalEmojiAsync(card, page);       break;
            case "Toggle Weekly-Only": await ToggleWeeklyOnlyAsync(card, page);    break;
            case "Delete":             await DeleteGoalAsync(card, page);          break;
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
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, newName, card.Goal.Points, card.Goal.IconEmoji, card.Goal.IsWeeklyOnly);
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
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, card.Goal.Name, newPoints, card.Goal.IconEmoji, card.Goal.IsWeeklyOnly);

        var record = await GoalService.GetTodayRecordAsync();
        await UpdateScoreAsync(record);
    }

    async Task EditGoalEmojiAsync(GoalCardViewModel card, Page page)
    {
        var newEmoji = await page.DisplayPromptAsync(
            "Edit Emoji", "Enter an emoji for this goal",
            initialValue: card.IconEmoji,
            maxLength: 8,
            keyboard: Keyboard.Default);

        if (newEmoji == null) return; // user cancelled

        newEmoji = newEmoji.Trim();
        if (newEmoji.Length == 0) newEmoji = "⭐";
        if (newEmoji == card.IconEmoji) return;

        card.Goal.IconEmoji = newEmoji;
        card.IconEmoji = newEmoji;
        card.Goal.IsDefault = false;
        await GoalService.SaveGoalAsync(card.Goal);
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, card.Goal.Name, card.Goal.Points, newEmoji, card.Goal.IsWeeklyOnly);
    }

    async Task ToggleWeeklyOnlyAsync(GoalCardViewModel card, Page page)
    {
        card.Goal.IsWeeklyOnly = !card.Goal.IsWeeklyOnly;
        card.IsWeeklyOnly = card.Goal.IsWeeklyOnly;
        card.Goal.IsDefault = false;
        await GoalService.SaveGoalAsync(card.Goal);
        await GoalService.UpdateTodayGoalSnapshotAsync(card.Goal.Id, card.Goal.Name, card.Goal.Points, card.Goal.IconEmoji, card.Goal.IsWeeklyOnly);

        var record = await GoalService.GetTodayRecordAsync();
        await UpdateScoreAsync(record);
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
        await UpdateScoreAsync(record);
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

        var emojiInput = await page.DisplayPromptAsync(
            "New Goal", $"Enter an emoji icon for \"{name}\"",
            initialValue: "⭐",
            maxLength: 8,
            keyboard: Keyboard.Default);

        if (emojiInput == null) return; // user cancelled

        var emoji = emojiInput.Trim();
        if (emoji.Length == 0) emoji = "⭐";

        bool isWeeklyOnly = await page.DisplayAlertAsync(
            "New Goal",
            "Should this goal count toward the weekly score only (not the daily score)?",
            "Weekly Only", "Daily");

        var newGoal = new Goal
        {
            Name = name,
            IconEmoji = emoji,
            Points = pts,
            IsWeeklyOnly = isWeeklyOnly,
            SortOrder = Goals.Count
        };

        await GoalService.SaveGoalAsync(newGoal);

        // Reload so GetTodayRecordAsync reconciles the new goal into today's entries.
        await LoadAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    async Task UpdateScoreAsync(DailyRecord record)
    {
        DailyScoreText = $"Today: {record.TotalPointsEarned} / {record.TotalPointsPossible}";

        var weekStart = GetMondayOfCurrentWeek();
        var (_, weeklyPercent) = await GoalService.GetWeeklyScoreAsync("local", weekStart);
        WeeklyScoreText = $"This week: {(int)Math.Round(weeklyPercent)}%";

        // All-goals-complete only considers non-weekly goals.
        AllGoalsCompleted = Goals.Count > 0 && Goals.Where(g => !g.IsWeeklyOnly).All(g => g.IsCompleted);
    }

    static DateOnly GetMondayOfCurrentWeek()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-daysFromMonday);
    }

    static Page GetCurrentPage() =>
        Application.Current!.Windows[0].Page
            ?? throw new InvalidOperationException("No active page found.");
}
