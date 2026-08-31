using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class DailyGoalScoringTests
{
    [Fact]
    public async Task GetTodayRecordAsync_DefaultGoalsProvideFourteenDailyPointsAndOneWeeklyGoal()
    {
        var databasePath = DatabaseTestSupport.CreatePath("daily");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var entries = await service.GetDailyEntriesAsync(record.Id);

            Assert.Equal(14, record.TotalPointsPossible);
            Assert.Equal(7, entries.Count(item => !item.IsWeeklyOnly));
            Assert.Single(entries, item => item.IsWeeklyOnly);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task ToggleGoalCompletionAsync_WeeklyGoalDoesNotChangeDailyPoints()
    {
        var databasePath = DatabaseTestSupport.CreatePath("daily");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var weeklyGoal = Assert.Single(
                await service.GetGoalsAsync(),
                item => item.IsWeeklyOnly);

            await service.ToggleGoalCompletionAsync(weeklyGoal.Id);

            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.NotNull(updated);
            Assert.Equal(0, updated.TotalPointsEarned);
            Assert.Equal(14, updated.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task ToggleGoalCompletionAsync_DailyGoalAddsItsConfiguredPoints()
    {
        var databasePath = DatabaseTestSupport.CreatePath("daily");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.GetTodayRecordAsync();
            var sleepGoal = Assert.Single(
                await service.GetGoalsAsync(),
                item => item.Name == "Slept at least 7 hours");

            await service.ToggleGoalCompletionAsync(sleepGoal.Id);

            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.NotNull(updated);
            Assert.Equal(3, updated.TotalPointsEarned);
            Assert.Equal(14, updated.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }
}
