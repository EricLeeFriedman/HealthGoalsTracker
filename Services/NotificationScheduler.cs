using HealthGoalsTracker.Models;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace HealthGoalsTracker.Services;

public class NotificationScheduler : IHealthNotificationService
{
    public IGoalService GoalService;
    public ILocalNotificationGateway NotificationGateway;
    public ILogger<NotificationScheduler> Logger;

    static readonly int NudgeBaseId = 1000;
    static readonly int SummaryId   = 2000;
    static readonly int RecapId     = 3000;

    public NotificationScheduler(
        IGoalService goalService,
        ILocalNotificationGateway notificationGateway,
        ILogger<NotificationScheduler> logger)
    {
        GoalService = goalService;
        NotificationGateway = notificationGateway;
        Logger = logger;
    }

    public async Task RescheduleAllAsync()
    {
        if (!NotificationGateway.IsSupported)
        {
            Logger.LogInformation("Notification scheduling skipped on the current development target");
            return;
        }

        var settings = await GoalService.GetUserSettingsAsync();
        if (!settings.NotificationsEnabled)
        {
            await CancelAllAsync();
            Logger.LogInformation("Notification scheduling disabled in application settings");
            return;
        }

        var permissionGranted =
            await NotificationGateway.AreNotificationsEnabledAsync();
        if (!permissionGranted)
            permissionGranted =
                await NotificationGateway.RequestPermissionAsync();

        if (!permissionGranted)
        {
            Logger.LogWarning("Notification permission denied; schedules were not created");
            return;
        }

        var schedules = await GoalService.GetNotificationSchedulesAsync();
        var todayRecord = await GoalService.GetTodayRecordAsync();
        var todayEntries = await GoalService.GetDailyEntriesAsync(todayRecord.Id);
        var completedToday = todayEntries.Count(entry => entry.IsCompleted);
        NotificationGateway.CancelAll();

        foreach (var schedule in schedules.Where(s => s.IsEnabled).OrderBy(s => s.SortOrder))
        {
            if (schedule.Type == NotificationType.NudgeIfNoGoalsCompleted &&
                completedToday > 0)
                continue;

            switch (schedule.Type)
            {
                case NotificationType.NudgeIfNoGoalsCompleted:
                    await ScheduleNudgeAsync(schedule);
                    break;
                case NotificationType.DailySummary:
                    await ScheduleDailySummaryAsync(schedule);
                    break;
                case NotificationType.MorningRecap:
                    await ScheduleMorningRecapAsync(schedule);
                    break;
            }
        }

        Logger.LogInformation("Notification schedules created");
    }

    public Task CancelNudgesAsync()
    {
        NotificationGateway.Cancel(NudgeBaseId);
        NotificationGateway.Cancel(NudgeBaseId + 1);
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        NotificationGateway.CancelAll();
        return Task.CompletedTask;
    }

    public async Task ScheduleNudgeAsync(NotificationSchedule schedule)
    {
        var request = new NotificationRequest
        {
            NotificationId = NudgeBaseId + schedule.SortOrder,
            Title          = "Don't forget your health goals! 💪",
            Description    = "You haven't logged any goals yet today. Tap to check in.",
            Schedule       =
            {
                NotifyTime  = NextOccurrence(schedule.HourOfDay, schedule.MinuteOfHour),
                RepeatType  = NotificationRepeat.Daily,
                Android     = { AlarmType = AndroidAlarmType.RtcWakeup }
            },
            Android        = { ChannelId = "health_goals" }
        };
        await NotificationGateway.ShowAsync(request);
    }

    public async Task ScheduleDailySummaryAsync(NotificationSchedule schedule)
    {
        var request = new NotificationRequest
        {
            NotificationId = SummaryId,
            Title          = "Daily Health Goals Summary 🏁",
            Description    = "Check how you did with your health goals today.",
            Schedule       =
            {
                NotifyTime  = NextOccurrence(schedule.HourOfDay, schedule.MinuteOfHour),
                RepeatType  = NotificationRepeat.Daily,
                Android     = { AlarmType = AndroidAlarmType.RtcWakeup }
            },
            Android        = { ChannelId = "health_goals" }
        };
        await NotificationGateway.ShowAsync(request);
    }

    public async Task ScheduleMorningRecapAsync(NotificationSchedule schedule)
    {
        var request = new NotificationRequest
        {
            NotificationId = RecapId,
            Title          = "Good morning! Yesterday's recap 🌅",
            Description    = "See how you did with your health goals yesterday.",
            Schedule       =
            {
                NotifyTime  = NextOccurrence(schedule.HourOfDay, schedule.MinuteOfHour),
                RepeatType  = NotificationRepeat.Daily,
                Android     = { AlarmType = AndroidAlarmType.RtcWakeup }
            },
            Android        = { ChannelId = "health_goals" }
        };
        await NotificationGateway.ShowAsync(request);
    }

    public static DateTime NextOccurrence(int hour, int minute) =>
        NextOccurrence(hour, minute, DateTime.Now);

    public static DateTime NextOccurrence(int hour, int minute, DateTime now)
    {
        var candidate = now.Date.AddHours(hour).AddMinutes(minute);
        if (candidate <= now)
            candidate = candidate.AddDays(1);
        return candidate;
    }
}
