using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Tests;

public class NotificationViewModelTests
{
    [Fact]
    public async Task LoadAsync_ExposesAllConfiguredNotificationTypesInDisplayOrder()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notifications");
        var goalService = new LocalGoalService(databasePath);
        var notificationService = new RecordingNotificationService();
        var viewModel = new NotificationsViewModel(goalService, notificationService);

        try
        {
            await viewModel.LoadAsync();

            Assert.True(viewModel.NotificationsEnabled);
            Assert.Equal(
                [
                    Models.NotificationType.NudgeIfNoGoalsCompleted,
                    Models.NotificationType.NudgeIfNoGoalsCompleted,
                    Models.NotificationType.DailySummary,
                    Models.NotificationType.MorningRecap
                ],
                viewModel.Items.Select(item => item.Schedule.Type));
            Assert.Equal(
                [new TimeSpan(12, 0, 0), new TimeSpan(16, 0, 0), new TimeSpan(21, 0, 0), new TimeSpan(7, 0, 0)],
                viewModel.Items.Select(item => item.Time));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task ToggleAllCommand_PersistsMasterSettingAndReschedulesNotifications()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notifications");
        var goalService = new LocalGoalService(databasePath);
        var notificationService = new RecordingNotificationService();
        var viewModel = new NotificationsViewModel(goalService, notificationService);

        try
        {
            await viewModel.ToggleAllCommand.ExecuteAsync(false);

            Assert.False((await goalService.GetUserSettingsAsync()).NotificationsEnabled);
            Assert.Equal(1, notificationService.RescheduleCount);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public void NotificationItem_UpdatesBackingScheduleWhenEdited()
    {
        var changes = 0;
        var item = new NotificationItemViewModel
        {
            Schedule = new() { IsEnabled = true },
            IsEnabled = true,
            OnChanged = () => changes++
        };

        item.IsEnabled = false;
        item.Time = new TimeSpan(18, 45, 0);

        Assert.False(item.Schedule.IsEnabled);
        Assert.Equal(new TimeOnly(18, 45), item.Schedule.Time);
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task EditingNotificationItem_PersistsScheduleAndReschedulesNotifications()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notifications");
        var goalService = new LocalGoalService(databasePath);
        var notificationService = new RecordingNotificationService();
        var viewModel = new NotificationsViewModel(goalService, notificationService);

        try
        {
            await viewModel.LoadAsync();
            var summary = Assert.Single(
                viewModel.Items,
                item => item.Schedule.Type == Models.NotificationType.DailySummary);

            summary.Time = new TimeSpan(20, 15, 0);
            await FeatureTestAssertions.WaitUntilAsync(
                () => notificationService.RescheduleCount == 1);

            var saved = Assert.Single(
                await goalService.GetNotificationSchedulesAsync(),
                schedule => schedule.Id == summary.Schedule.Id);
            Assert.Equal(new TimeOnly(20, 15), saved.Time);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

}

public class RecordingNotificationService : IHealthNotificationService
{
    public int RescheduleCount { get; set; }
    public int CancelNudgesCount { get; set; }
    public int CancelAllCount { get; set; }

    public Task RescheduleAllAsync()
    {
        RescheduleCount++;
        return Task.CompletedTask;
    }

    public Task CancelNudgesAsync()
    {
        CancelNudgesCount++;
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        CancelAllCount++;
        return Task.CompletedTask;
    }
}
