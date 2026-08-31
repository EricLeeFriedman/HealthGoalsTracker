using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Tests;

public class WeeklyScoringTests
{
    [Fact]
    public async Task GetWeeklyScoreAsync_AveragesDaysWithDataAndCapsWeeklySessionsAtThree()
    {
        var databasePath = DatabaseTestSupport.CreatePath("scoring");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var monday = new DateOnly(2026, 8, 24);
            var mondayRecord = CreateRecord(monday, 14);
            var tuesdayRecord = CreateRecord(monday.AddDays(1), 7);
            var wednesdayRecord = CreateRecord(monday.AddDays(2), 14);
            var thursdayRecord = CreateRecord(monday.AddDays(3), 7);
            await service.Database.InsertAllAsync(
                new[] { mondayRecord, tuesdayRecord, wednesdayRecord, thursdayRecord });

            await service.Database.InsertAllAsync(
            new[]
            {
                CreateWeeklyEntry(mondayRecord.Id),
                CreateWeeklyEntry(tuesdayRecord.Id),
                CreateWeeklyEntry(wednesdayRecord.Id),
                CreateWeeklyEntry(thursdayRecord.Id)
            });

            var (score, percent) = await service.GetWeeklyScoreAsync("local", monday);

            Assert.Equal(13.5, score);
            Assert.Equal(13.5 / 17 * 100, percent, 6);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetWeeklyScoreAsync_NoDaysWithDataReturnsZero()
    {
        var databasePath = DatabaseTestSupport.CreatePath("scoring");
        var service = new LocalGoalService(databasePath);

        try
        {
            var (score, percent) = await service.GetWeeklyScoreAsync(
                "local",
                new DateOnly(2026, 8, 24));

            Assert.Equal(0, score);
            Assert.Equal(0, percent);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetWeeklyScoreAsync_ExcludesIncompleteAndOutOfWeekTrainingSessions()
    {
        var databasePath = DatabaseTestSupport.CreatePath("scoring");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var monday = new DateOnly(2026, 8, 24);
            var inWeek = CreateRecord(monday, 14);
            var beforeWeek = CreateRecord(monday.AddDays(-1), 14);
            await service.Database.InsertAllAsync(new[] { inWeek, beforeWeek });
            await service.Database.InsertAllAsync(
                new[]
                {
                    CreateWeeklyEntry(inWeek.Id, false),
                    CreateWeeklyEntry(beforeWeek.Id, true)
                });

            var (score, percent) = await service.GetWeeklyScoreAsync("local", monday);

            Assert.Equal(14, score);
            Assert.Equal(14d / 17 * 100, percent, 6);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetWeeklyScoreAsync_IgnoresRecordsForOtherUsers()
    {
        var databasePath = DatabaseTestSupport.CreatePath("scoring");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var monday = new DateOnly(2026, 8, 24);
            await service.Database.InsertAllAsync(
            new[]
            {
                CreateRecord(monday, 14, "local"),
                CreateRecord(monday, 1, "other-user")
            });

            var (score, _) = await service.GetWeeklyScoreAsync("local", monday);

            Assert.Equal(14, score);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Theory]
    [InlineData(2026, 8, 24, 2026, 8, 24)]
    [InlineData(2026, 8, 30, 2026, 8, 24)]
    [InlineData(2026, 8, 23, 2026, 8, 17)]
    public void GetWeekStart_ReturnsMonday(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var result = HistoryViewModel.GetWeekStart(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }

    public static DailyRecord CreateRecord(
        DateOnly date,
        int pointsEarned,
        string userId = "local") =>
        new()
        {
            UserId = userId,
            Date = date.ToString("yyyy-MM-dd"),
            TotalPointsEarned = pointsEarned,
            TotalPointsPossible = 14
        };

    public static DailyGoalEntry CreateWeeklyEntry(string recordId, bool isCompleted = true) =>
        new()
        {
            DailyRecordId = recordId,
            GoalId = Guid.NewGuid().ToString(),
            GoalName = "Strength Training",
            IsWeeklyOnly = true,
            IsCompleted = isCompleted
        };

}
