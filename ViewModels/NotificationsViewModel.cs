using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.ViewModels
{
    public partial class NotificationItemViewModel : ObservableObject
    {
        public NotificationSchedule Schedule;

        [ObservableProperty]
        string _label = "";

        [ObservableProperty]
        bool _isEnabled;

        [ObservableProperty]
        TimeSpan _time;

        partial void OnIsEnabledChanged(bool value)
        {
            Schedule.IsEnabled = value;
            Schedule.UpdatedAt = DateTime.UtcNow;
            OnChanged?.Invoke();
        }

        partial void OnTimeChanged(TimeSpan value)
        {
            Schedule.Hour   = value.Hours;
            Schedule.Minute = value.Minutes;
            Schedule.UpdatedAt = DateTime.UtcNow;
            OnChanged?.Invoke();
        }

        public Action? OnChanged;
    }

    public partial class NotificationsViewModel : ObservableObject
    {
        public IGoalService GoalService;
        public IHealthNotificationService NotificationService;

        [ObservableProperty]
        bool _notificationsEnabled;

        [ObservableProperty]
        ObservableCollection<NotificationItemViewModel> _items = new();

        public NotificationsViewModel(IGoalService goalService, IHealthNotificationService notificationService)
        {
            GoalService = goalService;
            NotificationService = notificationService;
        }

        public async Task LoadAsync()
        {
            var settings = await GoalService.GetUserSettingsAsync();
            NotificationsEnabled = settings.NotificationsEnabled;

            var schedules = await GoalService.GetNotificationSchedulesAsync();
            Items.Clear();

            foreach (var schedule in schedules.OrderBy(s => s.SortOrder))
            {
                var item = new NotificationItemViewModel
                {
                    Schedule   = schedule,
                    Label      = DescribeSchedule(schedule),
                    IsEnabled  = schedule.IsEnabled,
                    Time       = new TimeSpan(schedule.Hour, schedule.Minute, 0),
                    OnChanged  = null // set after to avoid triggering during init
                };
                item.OnChanged = async () => await SaveItemAsync(item);
                Items.Add(item);
            }
        }

        [RelayCommand]
        async Task ToggleAll(bool enabled)
        {
            NotificationsEnabled = enabled;
            var settings = await GoalService.GetUserSettingsAsync();
            settings.NotificationsEnabled = enabled;
            await GoalService.SaveUserSettingsAsync(settings);
            await NotificationService.RescheduleAllAsync();
        }

        async Task SaveItemAsync(NotificationItemViewModel item)
        {
            await GoalService.SaveNotificationScheduleAsync(item.Schedule);
            await NotificationService.RescheduleAllAsync();
        }

        static string DescribeSchedule(NotificationSchedule s) =>
            s.Type switch
            {
                NotificationType.NudgeIfNoGoalsCompleted when s.SortOrder == 0 => "Nudge — first reminder",
                NotificationType.NudgeIfNoGoalsCompleted => "Nudge — second reminder",
                NotificationType.DailySummary => "Daily summary reminder",
                NotificationType.MorningRecap => "Morning recap",
                _ => s.Type.ToString()
            };
    }
}

