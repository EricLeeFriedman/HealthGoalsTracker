using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthGoalsTracker.ViewModels
{
    public partial class CalendarDayViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutomationId))]
        public partial DateOnly Date { get; set; }

        // True for padding cells at the start of the month (no data, no tap)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AutomationId))]
        [NotifyPropertyChangedFor(nameof(IsSelectable))]
        public partial bool IsEmpty { get; set; }

        // True when the date is in the future
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSelectable))]
        public partial bool IsFuture { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        // Null → no data recorded (light grey)
        [ObservableProperty]
        public partial double? CompletionPercent { get; set; }

        public string DayLabel => IsEmpty ? "" : Date.Day.ToString();
        public string AutomationId =>
            IsEmpty ? "HistoryPaddingDay" : $"HistoryDay{Date:yyyyMMdd}";
        public bool IsSelectable => !IsEmpty && !IsFuture;

        public Color BackgroundColor
        {
            get
            {
                if (IsEmpty) return Colors.Transparent;
                if (IsFuture) return Color.FromArgb("#E0E0E0");
                if (CompletionPercent == null) return Colors.Transparent;
                var pct = CompletionPercent.Value;
                if (pct >= 100) return Color.FromArgb("#4CAF50");  // green
                if (pct >= 50)  return Color.FromArgb("#FFC107");  // amber
                if (pct > 0)    return Color.FromArgb("#FF9800");  // orange
                return Color.FromArgb("#F44336");                   // red
            }
        }

        public Color BorderColor => IsSelected ? Color.FromArgb("#512BD4") : Colors.Transparent;
    }
}
