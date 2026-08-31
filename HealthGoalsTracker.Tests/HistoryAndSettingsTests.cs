using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class HistoryAndSettingsTests
{
    [Fact]
    public async Task GetRecordsForRangeAsync_ReturnsOnlyInclusiveRequestedDatesInOrder()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            await service.Database.InsertAllAsync(
            new[]
            {
                CreateRecord("2026-08-01"),
                CreateRecord("2026-08-02"),
                CreateRecord("2026-08-03"),
                CreateRecord("2026-08-04"),
                CreateRecord("2026-08-02", "another-user")
            });

            var records = await service.GetRecordsForRangeAsync(
                new DateOnly(2026, 8, 2),
                new DateOnly(2026, 8, 3));

            Assert.Equal(["2026-08-02", "2026-08-03"], records.Select(record => record.Date));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetTodayRecordAsync_CreatesANewDayWithoutChangingHistory()
    {
        var databasePath = DatabaseTestSupport.CreatePath("history");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var yesterday = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
            var historical = CreateRecord(yesterday.ToString("yyyy-MM-dd"));
            historical.TotalPointsEarned = 7;
            await service.Database.InsertAsync(historical);

            var today = await service.GetTodayRecordAsync();
            var unchangedHistory = await service.GetRecordForDateAsync(yesterday);

            Assert.NotEqual(historical.Id, today.Id);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), today.Date);
            Assert.NotNull(unchangedHistory);
            Assert.Equal(7, unchangedHistory.TotalPointsEarned);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task UserSettings_DefaultToNotificationsEnabledAndPersistChanges()
    {
        var databasePath = DatabaseTestSupport.CreatePath("settings");
        var service = new LocalGoalService(databasePath);

        try
        {
            var settings = await service.GetUserSettingsAsync();
            Assert.Equal("local", settings.UserId);
            Assert.True(settings.NotificationsEnabled);

            settings.NotificationsEnabled = false;
            await service.SaveUserSettingsAsync(settings);

            Assert.False((await service.GetUserSettingsAsync()).NotificationsEnabled);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task NotificationSchedules_UseDocumentedDefaultsAndPersistEdits()
    {
        var databasePath = DatabaseTestSupport.CreatePath("settings");
        var service = new LocalGoalService(databasePath);

        try
        {
            var schedules = await service.GetNotificationSchedulesAsync();

            Assert.Collection(
                schedules,
                schedule => FeatureTestAssertions.AssertSchedule(schedule, NotificationType.NudgeIfNoGoalsCompleted, 12, 0),
                schedule => FeatureTestAssertions.AssertSchedule(schedule, NotificationType.NudgeIfNoGoalsCompleted, 16, 0),
                schedule => FeatureTestAssertions.AssertSchedule(schedule, NotificationType.DailySummary, 21, 0),
                schedule => FeatureTestAssertions.AssertSchedule(schedule, NotificationType.MorningRecap, 7, 0));

            var dailySummary = schedules.Single(schedule => schedule.Type == NotificationType.DailySummary);
            dailySummary.HourOfDay = 20;
            dailySummary.MinuteOfHour = 30;
            dailySummary.IsEnabled = false;
            await service.SaveNotificationScheduleAsync(dailySummary);

            var saved = (await service.GetNotificationSchedulesAsync())
                .Single(schedule => schedule.Id == dailySummary.Id);
            Assert.Equal(new TimeOnly(20, 30), saved.Time);
            Assert.False(saved.IsEnabled);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task UpdateUserIdAsync_ClaimsLocalGoalsRecordsAndSettings()
    {
        var databasePath = DatabaseTestSupport.CreatePath("identity");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.GetTodayRecordAsync();
            await service.GetUserSettingsAsync();

            await service.UpdateUserIdAsync("signed-in-user");

            Assert.All(
                await service.Database.Table<Goal>().ToListAsync(),
                goal => Assert.Equal("signed-in-user", goal.UserId));
            Assert.All(
                await service.Database.Table<DailyRecord>().ToListAsync(),
                record => Assert.Equal("signed-in-user", record.UserId));
            Assert.All(
                await service.Database.Table<UserSettings>().ToListAsync(),
                settings => Assert.Equal("signed-in-user", settings.UserId));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    public static DailyRecord CreateRecord(string date, string userId = "local") =>
        new()
        {
            UserId = userId,
            Date = date,
            TotalPointsPossible = 14
        };
}
