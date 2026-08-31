using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Tests;

public class HistoryViewModelTests
{
    [Fact]
    public async Task LoadAsync_BuildsFullMonthWithSundayAlignedPadding()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);

        try
        {
            await viewModel.LoadAsync();

            var firstOfMonth = new DateOnly(
                viewModel.DisplayMonth.Year,
                viewModel.DisplayMonth.Month,
                1);
            var expectedPadding = (int)firstOfMonth.DayOfWeek;
            var expectedDays = DateTime.DaysInMonth(
                viewModel.DisplayMonth.Year,
                viewModel.DisplayMonth.Month);

            Assert.Equal(expectedPadding + expectedDays, viewModel.Days.Count);
            Assert.Equal(expectedPadding, viewModel.Days.Count(day => day.IsEmpty));
            Assert.Equal(
                Enumerable.Range(1, expectedDays),
                viewModel.Days.Where(day => !day.IsEmpty).Select(day => day.Date.Day));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task SelectDayCommand_ShowsDailyBreakdownAndCanonicalWeeklyPercentage()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var sleep = Assert.Single(
                await service.GetGoalsAsync(),
                goal => goal.Name == "Slept at least 7 hours");
            await service.ToggleGoalCompletionAsync(sleep.Id);
            await viewModel.LoadAsync();
            var today = Assert.Single(
                viewModel.Days,
                day => !day.IsEmpty && day.Date == DateOnly.FromDateTime(DateTime.Today));

            await viewModel.SelectDayCommand.ExecuteAsync(today);

            Assert.True(viewModel.HasSelectedDay);
            Assert.Equal(8, viewModel.SelectedDayGoals.Count);
            Assert.Single(
                viewModel.SelectedDayGoals,
                goal => goal.GoalName == sleep.Name && goal.IsCompleted);
            Assert.Equal("3/14 pts — 21%", viewModel.SelectedDaySummary);
            Assert.StartsWith("This week: 18%", viewModel.SelectedWeekSummary);
            Assert.Equal(record.Date, viewModel.SelectedDay!.Date.ToString("yyyy-MM-dd"));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task SelectDayCommand_SelectingSameDayAgainCollapsesBreakdown()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);

        try
        {
            await service.GetTodayRecordAsync();
            await viewModel.LoadAsync();
            var today = Assert.Single(
                viewModel.Days,
                day => !day.IsEmpty && day.Date == DateOnly.FromDateTime(DateTime.Today));

            await viewModel.SelectDayCommand.ExecuteAsync(today);
            await viewModel.SelectDayCommand.ExecuteAsync(today);

            Assert.False(viewModel.HasSelectedDay);
            Assert.Null(viewModel.SelectedDay);
            Assert.Empty(viewModel.SelectedDayGoals);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task PreviousMonthCommand_MovesToPreviousCalendarMonth()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);
        var currentMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        try
        {
            await viewModel.PreviousMonthCommand.ExecuteAsync(null);
            Assert.Equal(currentMonth.AddMonths(-1), new DateOnly(
                viewModel.DisplayMonth.Year,
                viewModel.DisplayMonth.Month,
                1));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task NextMonthCommand_DoesNotMovePastCurrentMonth()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);
        var currentMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        try
        {
            await viewModel.NextMonthCommand.ExecuteAsync(null);

            Assert.Equal(currentMonth, new DateOnly(
                viewModel.DisplayMonth.Year,
                viewModel.DisplayMonth.Month,
                1));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task SelectDayCommand_NoDataShowsEmptyDayMessageAndWeeklySummary()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);

        try
        {
            viewModel.DisplayMonth = viewModel.DisplayMonth.AddMonths(-1);
            await viewModel.LoadAsync();
            var day = viewModel.Days.First(item => !item.IsEmpty);

            await viewModel.SelectDayCommand.ExecuteAsync(day);

            Assert.True(viewModel.HasSelectedDay);
            Assert.Equal("No data recorded for this day.", viewModel.SelectedDaySummary);
            Assert.StartsWith("This week: 0%", viewModel.SelectedWeekSummary);
            Assert.Empty(viewModel.SelectedDayGoals);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_MapsDailyRecordCompletionToItsCalendarDay()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history-view-model");
        var service = new LocalGoalService(databasePath);
        var viewModel = new HistoryViewModel(service);

        try
        {
            var record = await service.GetTodayRecordAsync();
            record.TotalPointsEarned = 7;
            await service.Database.UpdateAsync(record);

            await viewModel.LoadAsync();

            var today = Assert.Single(
                viewModel.Days,
                day => !day.IsEmpty && day.Date == DateOnly.FromDateTime(DateTime.Today));
            Assert.Equal(50, today.CompletionPercent);
            Assert.False(today.IsFuture);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }
}
