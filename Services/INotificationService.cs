namespace HealthGoalsTracker.Services;

public interface IHealthNotificationService
{
    // Reschedule all notifications from persisted schedules.
    Task RescheduleAllAsync();

    // Cancel nudge notifications (called when first goal is completed for the day).
    Task CancelNudgesAsync();

    // Cancel all notifications.
    Task CancelAllAsync();
}
