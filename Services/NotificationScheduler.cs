using HealthGoalsTracker.Models;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace HealthGoalsTracker.Services;

public class NotificationScheduler : IHealthNotificationService
{
    public IGoalService GoalService;
    public ILogger<NotificationScheduler> Logger;

    static readonly int NudgeBaseId = 1000;
    static readonly int SummaryId   = 2000;
    static readonly int RecapId     = 3000;

    public NotificationScheduler(
        IGoalService goalService,
        ILogger<NotificationScheduler> logger)
    {
        GoalService = goalService;
        Logger = logger;
    }

    public async Task RescheduleAllAsync()
    {
        var settings = await GoalService.GetUserSettingsAsync();
        if (!settings.NotificationsEnabled)
        {
            await CancelAllAsync();
            Logger.LogInformation("Notification scheduling disabled in application settings");
            return;
        }

        var permissionGranted =
            await LocalNotificationCenter.Current.AreNotificationsEnabled();
        if (!permissionGranted)
            permissionGranted =
                await LocalNotificationCenter.Current.RequestNotificationPermission();

        if (!permissionGranted)
        {
            Logger.LogWarning("Notification permission denied; schedules were not created");
            return;
        }

        var schedules = await GoalService.GetNotificationSchedulesAsync();
        LocalNotificationCenter.Current.CancelAll();

        foreach (var schedule in schedules.Where(s => s.IsEnabled).OrderBy(s => s.SortOrder))
        {
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
        LocalNotificationCenter.Current.Cancel(NudgeBaseId);
        LocalNotificationCenter.Current.Cancel(NudgeBaseId + 1);
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    async Task ScheduleNudgeAsync(NotificationSchedule schedule)
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
        await LocalNotificationCenter.Current.Show(request);
    }

    async Task ScheduleDailySummaryAsync(NotificationSchedule schedule)
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
        await LocalNotificationCenter.Current.Show(request);
    }

    async Task ScheduleMorningRecapAsync(NotificationSchedule schedule)
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
        await LocalNotificationCenter.Current.Show(request);
    }

    static DateTime NextOccurrence(int hour, int minute)
    {
        var candidate = DateTime.Today.AddHours(hour).AddMinutes(minute);
        if (candidate <= DateTime.Now)
            candidate = candidate.AddDays(1);
        return candidate;
    }
}

