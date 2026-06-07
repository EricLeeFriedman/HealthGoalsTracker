using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthGoalsTracker.ViewModels
{
    public partial class CalendarDayViewModel : ObservableObject
    {
        [ObservableProperty]
        DateOnly _date;

        // True for padding cells at the start of the month (no data, no tap)
        [ObservableProperty]
        bool _isEmpty;

        // True when the date is in the future
        [ObservableProperty]
        bool _isFuture;

        [ObservableProperty]
        bool _isSelected;

        // Null → no data recorded (light grey)
        [ObservableProperty]
        double? _completionPercent;

        public string DayLabel => IsEmpty ? "" : Date.Day.ToString();

        public Color BackgroundColor
        {
            get
            {
                if (IsEmpty || IsFuture) return Colors.Transparent;
                if (CompletionPercent == null) return Color.FromArgb("#E0E0E0");
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
