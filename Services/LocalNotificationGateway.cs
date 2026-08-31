using Plugin.LocalNotification;

namespace HealthGoalsTracker.Services;

public interface ILocalNotificationGateway
{
    bool IsSupported { get; }
    Task<bool> AreNotificationsEnabledAsync();
    Task<bool> RequestPermissionAsync();
    Task ShowAsync(NotificationRequest request);
    void Cancel(int notificationId);
    void CancelAll();
}

public class LocalNotificationGateway : ILocalNotificationGateway
{
    public bool IsSupported =>
#if WINDOWS
        false;
#else
        true;
#endif

    public Task<bool> AreNotificationsEnabledAsync() =>
        LocalNotificationCenter.Current.AreNotificationsEnabled();

    public Task<bool> RequestPermissionAsync() =>
        LocalNotificationCenter.Current.RequestNotificationPermission();

    public Task ShowAsync(NotificationRequest request) =>
        LocalNotificationCenter.Current.Show(request);

    public void Cancel(int notificationId) =>
        LocalNotificationCenter.Current.Cancel(notificationId);

    public void CancelAll() =>
        LocalNotificationCenter.Current.CancelAll();
}
