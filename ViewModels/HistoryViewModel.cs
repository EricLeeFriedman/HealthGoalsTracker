using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.ViewModels
{
    public partial class GoalBreakdownItem : ObservableObject
    {
        [ObservableProperty]
        string _goalName = "";

        [ObservableProperty]
        int _goalPoints;

        [ObservableProperty]
        bool _isCompleted;

        public string StatusIcon => IsCompleted ? "✅" : "❌";
    }

    public partial class HistoryViewModel : ObservableObject
    {
        public IGoalService GoalService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MonthLabel))]
        DateOnly _displayMonth;

        [ObservableProperty]
        ObservableCollection<CalendarDayViewModel> _days = new();

        [ObservableProperty]
        ObservableCollection<GoalBreakdownItem> _selectedDayGoals = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedDay))]
        CalendarDayViewModel? _selectedDay;

        [ObservableProperty]
        string _selectedDayLabel = "";

        [ObservableProperty]
        string _selectedDaySummary = "";

        [ObservableProperty]
        string _selectedWeekScoreText = "";

        public bool HasSelectedDay => SelectedDay != null && !SelectedDay.IsEmpty && !SelectedDay.IsFuture;

        public string MonthLabel => DisplayMonth.ToString("MMMM yyyy");

        static readonly string[] WeekdayHeaders = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];

        public HistoryViewModel(IGoalService goalService)
        {
            GoalService = goalService;
            DisplayMonth = DateOnly.FromDateTime(DateTime.Today);
        }

        public async Task LoadAsync()
        {
            await BuildCalendarAsync();
        }

        [RelayCommand]
        async Task PreviousMonth()
        {
            DisplayMonth = DisplayMonth.AddMonths(-1);
            SelectedDay = null;
            SelectedDayGoals.Clear();
            SelectedWeekScoreText = "";
            await BuildCalendarAsync();
        }

        [RelayCommand]
        async Task NextMonth()
        {
            var next = DisplayMonth.AddMonths(1);
            // Don't navigate past current month
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (next > new DateOnly(today.Year, today.Month, 1)) return;
            DisplayMonth = next;
            SelectedDay = null;
            SelectedDayGoals.Clear();
            SelectedWeekScoreText = "";
            await BuildCalendarAsync();
        }

        [RelayCommand]
        async Task SelectDay(CalendarDayViewModel? day)
        {
            if (day == null || day.IsEmpty || day.IsFuture) return;

            // Deselect previous
            if (SelectedDay != null)
                SelectedDay.IsSelected = false;

            if (SelectedDay == day)
            {
                // Tap same day again → collapse
                SelectedDay = null;
                SelectedDayGoals.Clear();
                SelectedWeekScoreText = "";
                return;
            }

            day.IsSelected = true;
            SelectedDay = day;

            await LoadDayBreakdownAsync(day);
        }

        async Task BuildCalendarAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var firstDay = new DateOnly(DisplayMonth.Year, DisplayMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(DisplayMonth.Year, DisplayMonth.Month);
            var from = firstDay;
            var to = new DateOnly(DisplayMonth.Year, DisplayMonth.Month, daysInMonth);

            // Fetch records for this month in one query.
            var records = await GoalService.GetRecordsForRangeAsync(from, to);
            var recordMap = records.ToDictionary(r => r.Date);

            Days.Clear();

            // Pad to align first day of month with its weekday column (Sun=0).
            int startDow = (int)firstDay.DayOfWeek;
            for (int i = 0; i < startDow; i++)
                Days.Add(new CalendarDayViewModel { IsEmpty = true });

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateOnly(DisplayMonth.Year, DisplayMonth.Month, d);
                var key  = date.ToString("yyyy-MM-dd");

                double? pct = null;
                if (recordMap.TryGetValue(key, out var rec))
                    pct = rec.CompletionPercent;

                Days.Add(new CalendarDayViewModel
                {
                    Date              = date,
                    IsEmpty           = false,
                    IsFuture          = date > today,
                    CompletionPercent = pct
                });
            }
        }

        async Task LoadDayBreakdownAsync(CalendarDayViewModel day)
        {
            SelectedDayGoals.Clear();

            var weekStart = GetMonday(day.Date);
            var (_, weeklyPercent) = await GoalService.GetWeeklyScoreAsync("local", weekStart);
            SelectedWeekScoreText = $"This week: {(int)Math.Round(weeklyPercent)}%";

            var record = await GoalService.GetRecordForDateAsync(day.Date);
            if (record == null)
            {
                SelectedDayLabel   = day.Date.ToString("dddd, MMMM d");
                SelectedDaySummary = "No data recorded for this day.";
                return;
            }

            var entries = await GoalService.GetDailyEntriesAsync(record.Id);

            SelectedDayLabel = day.Date.ToString("dddd, MMMM d");
            SelectedDaySummary = $"{record.TotalPointsEarned}/{record.TotalPointsPossible} pts — {Math.Round(record.CompletionPercent, 0)}%";

            foreach (var entry in entries)
            {
                SelectedDayGoals.Add(new GoalBreakdownItem
                {
                    GoalName    = entry.GoalName,
                    GoalPoints  = entry.GoalPoints,
                    IsCompleted = entry.IsCompleted
                });
            }
        }

        static DateOnly GetMonday(DateOnly date)
        {
            int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysFromMonday);
        }
    }
}
