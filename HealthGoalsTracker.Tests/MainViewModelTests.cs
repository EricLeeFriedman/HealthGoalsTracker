using CommunityToolkit.Mvvm.Messaging;
using HealthGoalsTracker.Messages;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsAllDefaultCardsAndCanonicalScores()
    {
        var databasePath = DatabaseTestSupport.CreatePath("main-view-model");
        var goalService = new LocalGoalService(databasePath);
        var viewModel = new MainViewModel(goalService, new RecordingNotificationService());

        try
        {
            await viewModel.LoadAsync();

            Assert.Equal(8, viewModel.Goals.Count);
            Assert.Equal("Today: 0 / 14", viewModel.DailyScoreText);
            Assert.Equal("This week: 0%", viewModel.WeeklyScoreText);
            Assert.Equal(7, viewModel.Goals.Count(goal => !goal.IsWeeklyOnly));
            Assert.Single(viewModel.Goals, goal => goal.IsWeeklyOnly);
            Assert.All(viewModel.Goals, goal => Assert.False(goal.IsCompleted));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task DailyGoalCompletion_UpdatesCardAndScoreAndReschedulesNotifications()
    {
        var databasePath = DatabaseTestSupport.CreatePath("main-view-model");
        var goalService = new LocalGoalService(databasePath);
        var notificationService = new RecordingNotificationService();
        var viewModel = new MainViewModel(goalService, notificationService);

        try
        {
            await viewModel.LoadAsync();
            var sleep = Assert.Single(
                viewModel.Goals,
                goal => goal.Name == "Slept at least 7 hours");

            await sleep.ToggleCommand.ExecuteAsync(null);

            Assert.True(sleep.IsCompleted);
            Assert.Equal("Today: 3 / 14", viewModel.DailyScoreText);
            Assert.Equal(1, notificationService.RescheduleCount);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RemovingLastCompletion_ReschedulesConfiguredNudges()
    {
        var databasePath = DatabaseTestSupport.CreatePath("main-view-model");
        var goalService = new LocalGoalService(databasePath);
        var notificationService = new RecordingNotificationService();
        var viewModel = new MainViewModel(goalService, notificationService);

        try
        {
            await viewModel.LoadAsync();
            var sleep = Assert.Single(
                viewModel.Goals,
                goal => goal.Name == "Slept at least 7 hours");

            await sleep.ToggleCommand.ExecuteAsync(null);
            await sleep.ToggleCommand.ExecuteAsync(null);

            Assert.False(sleep.IsCompleted);
            Assert.Equal("Today: 0 / 14", viewModel.DailyScoreText);
            Assert.Equal(2, notificationService.RescheduleCount);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task CompletingAllDailyGoals_SendsAllGoalsCelebrationWithoutRequiringWeeklyGoal()
    {
        var databasePath = DatabaseTestSupport.CreatePath("main-view-model");
        var goalService = new LocalGoalService(databasePath);
        var viewModel = new MainViewModel(goalService, new RecordingNotificationService());
        var recipient = new object();
        var celebrations = new List<CelebrationMessage>();
        WeakReferenceMessenger.Default.Register<CelebrationMessage>(
            recipient,
            (_, message) => celebrations.Add(message));

        try
        {
            await viewModel.LoadAsync();
            foreach (var goal in viewModel.Goals.Where(goal => !goal.IsWeeklyOnly))
            {
                await goal.ToggleCommand.ExecuteAsync(null);
            }

            Assert.True(viewModel.AllGoalsCompleted);
            Assert.Equal("Today: 14 / 14", viewModel.DailyScoreText);
            Assert.False(Assert.Single(viewModel.Goals, goal => goal.IsWeeklyOnly).IsCompleted);
            Assert.Equal(7, celebrations.Count);
            Assert.False(celebrations[0].AllGoalsComplete);
            Assert.True(celebrations[^1].AllGoalsComplete);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task WeeklyGoalCompletion_ChangesWeeklyScoreWithoutChangingDailyScore()
    {
        var databasePath = DatabaseTestSupport.CreatePath("main-view-model");
        var goalService = new LocalGoalService(databasePath);
        var viewModel = new MainViewModel(goalService, new RecordingNotificationService());

        try
        {
            await viewModel.LoadAsync();
            var weeklyGoal = Assert.Single(viewModel.Goals, goal => goal.IsWeeklyOnly);

            await weeklyGoal.ToggleCommand.ExecuteAsync(null);

            Assert.True(weeklyGoal.IsCompleted);
            Assert.Equal("Today: 0 / 14", viewModel.DailyScoreText);
            Assert.Equal("This week: 6%", viewModel.WeeklyScoreText);
            Assert.False(viewModel.AllGoalsCompleted);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }
}
