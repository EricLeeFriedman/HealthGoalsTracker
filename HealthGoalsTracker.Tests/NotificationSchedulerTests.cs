using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Plugin.LocalNotification;

namespace HealthGoalsTracker.Tests;

public class NotificationSchedulerTests
{
    [Fact]
    public async Task RescheduleAllAsync_UnsupportedPlatformDoesNotAccessNotificationApis()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var gateway = new RecordingLocalNotificationGateway { IsSupported = false };
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(0, gateway.PermissionCheckCount);
            Assert.Equal(0, gateway.CancelAllCount);
            Assert.Empty(gateway.Requests);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_DefaultSettingsCreateFourDailyNotifications()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(1, gateway.CancelAllCount);
            Assert.Equal(4, gateway.Requests.Count);
            Assert.Equal([1000, 1001, 2000, 3000], gateway.Requests.Select(request => request.NotificationId));
            Assert.All(
                gateway.Requests,
                request => Assert.Equal(NotificationRepeat.Daily, request.Schedule.RepeatType));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_DisabledSettingCancelsWithoutRequestingPermission()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var settings = await goalService.GetUserSettingsAsync();
        settings.NotificationsEnabled = false;
        await goalService.SaveUserSettingsAsync(settings);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(1, gateway.CancelAllCount);
            Assert.Equal(0, gateway.PermissionCheckCount);
            Assert.Empty(gateway.Requests);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_DeniedPermissionCreatesNoSchedules()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var gateway = new RecordingLocalNotificationGateway
        {
            NotificationsEnabled = false,
            PermissionGranted = false
        };
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(1, gateway.PermissionCheckCount);
            Assert.Equal(1, gateway.PermissionRequestCount);
            Assert.Equal(0, gateway.CancelAllCount);
            Assert.Empty(gateway.Requests);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_AfterProgressSuppressesNudgesAndIncludesCurrentSummary()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        await goalService.GetTodayRecordAsync();
        var sleep = Assert.Single(
            await goalService.GetGoalsAsync(),
            goal => goal.Name == "Slept at least 7 hours");
        await goalService.ToggleGoalCompletionAsync(sleep.Id);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(2, gateway.Requests.Count);
            Assert.DoesNotContain(
                gateway.Requests,
                request => request.NotificationId is 1000 or 1001);
            var summary = Assert.Single(
                gateway.Requests,
                request => request.NotificationId == 2000);
            Assert.Equal("Check how you did with your health goals today.", summary.Description);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task CancelNudgesAsync_CancelsBothNudgeIdentifiers()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.CancelNudgesAsync();

            Assert.Equal([1000, 1001], gateway.CancelledIds);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_SkipsIndividuallyDisabledSchedule()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var schedules = await goalService.GetNotificationSchedulesAsync();
        var summary = Assert.Single(
            schedules,
            schedule => schedule.Type == NotificationType.DailySummary);
        summary.IsEnabled = false;
        await goalService.SaveNotificationScheduleAsync(summary);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            Assert.Equal(3, gateway.Requests.Count);
            Assert.DoesNotContain(
                gateway.Requests,
                request => request.NotificationId == 2000);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public async Task RescheduleAllAsync_MorningRecapUsesConfiguredRecapMessage()
    {
        var databasePath = DatabaseTestSupport.CreatePath("notification-scheduler");
        var goalService = new LocalGoalService(databasePath);
        var gateway = new RecordingLocalNotificationGateway();
        var scheduler = new NotificationScheduler(
            goalService,
            gateway,
            NullLogger<NotificationScheduler>.Instance);

        try
        {
            await scheduler.RescheduleAllAsync();

            var recap = Assert.Single(
                gateway.Requests,
                request => request.NotificationId == 3000);
            Assert.Equal(
                "See how you did with your health goals yesterday.",
                recap.Description);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(goalService, databasePath);
        }
    }

    [Fact]
    public void NextOccurrence_WhenTimeIsLaterTodayUsesToday()
    {
        var now = new DateTime(2026, 8, 31, 10, 0, 0);

        var next = NotificationScheduler.NextOccurrence(12, 30, now);

        Assert.Equal(new DateTime(2026, 8, 31, 12, 30, 0), next);
    }

    public class RecordingLocalNotificationGateway : ILocalNotificationGateway
    {
        public bool IsSupported { get; set; } = true;
        public bool NotificationsEnabled { get; set; } = true;
        public bool PermissionGranted { get; set; } = true;
        public int PermissionCheckCount { get; set; }
        public int PermissionRequestCount { get; set; }
        public int CancelAllCount { get; set; }
        public List<int> CancelledIds { get; set; } = [];
        public List<NotificationRequest> Requests { get; set; } = [];

        public Task<bool> AreNotificationsEnabledAsync()
        {
            PermissionCheckCount++;
            return Task.FromResult(NotificationsEnabled);
        }

        public Task<bool> RequestPermissionAsync()
        {
            PermissionRequestCount++;
            return Task.FromResult(PermissionGranted);
        }

        public Task ShowAsync(NotificationRequest request)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public void Cancel(int notificationId) =>
            CancelledIds.Add(notificationId);

        public void CancelAll() =>
            CancelAllCount++;
    }

    [Fact]
    public void NextOccurrence_WhenTimeHasPassedUsesTomorrow()
    {
        var now = new DateTime(2026, 8, 31, 22, 0, 0);

        var next = NotificationScheduler.NextOccurrence(7, 0, now);

        Assert.Equal(new DateTime(2026, 9, 1, 7, 0, 0), next);
    }

    [Fact]
    public void NextOccurrence_WhenTimeIsExactlyNowUsesTomorrow()
    {
        var now = new DateTime(2026, 8, 31, 12, 0, 0);

        var next = NotificationScheduler.NextOccurrence(12, 0, now);

        Assert.Equal(new DateTime(2026, 9, 1, 12, 0, 0), next);
    }
}
