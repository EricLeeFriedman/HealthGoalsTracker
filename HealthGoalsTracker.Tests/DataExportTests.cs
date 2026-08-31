using System.Text.Json;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class DataExportTests
{
    [Fact]
    public async Task BuildDataExportJsonAsync_ExportsAllDailyRecordsAndGoalSnapshots()
    {
        var databasePath = DatabaseTestSupport.CreatePath("export");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var record = new DailyRecord
            {
                UserId = "local",
                Date = "2019-12-31",
                TotalPointsEarned = 3,
                TotalPointsPossible = 14
            };
            await service.Database.InsertAsync(record);
            await service.Database.InsertAsync(new DailyGoalEntry
            {
                DailyRecordId = record.Id,
                GoalId = Guid.NewGuid().ToString(),
                GoalName = "Historical goal",
                IconEmoji = "⭐",
                GoalPoints = 3,
                IsWeeklyOnly = false,
                IsCompleted = true
            });
            await service.Database.InsertAsync(new DailyRecord
            {
                UserId = "local",
                Date = "2020-01-01",
                TotalPointsEarned = 1,
                TotalPointsPossible = 3
            });

            var json = await DataExportService.BuildJsonAsync(service);
            using var document = JsonDocument.Parse(json);
            var days = document.RootElement.GetProperty("days").EnumerateArray().ToList();
            Assert.Equal(2, days.Count);
            var day = days[0];
            var goal = Assert.Single(day.GetProperty("goals").EnumerateArray());

            Assert.Equal("2019-12-31", day.GetProperty("date").GetString());
            Assert.Equal(3, day.GetProperty("pointsEarned").GetInt32());
            Assert.Equal(14, day.GetProperty("pointsPossible").GetInt32());
            Assert.Equal("Historical goal", goal.GetProperty("name").GetString());
            Assert.Equal("⭐", goal.GetProperty("iconEmoji").GetString());
            Assert.Equal(3, goal.GetProperty("points").GetInt32());
            Assert.False(goal.GetProperty("isWeeklyOnly").GetBoolean());
            Assert.True(goal.GetProperty("completed").GetBoolean());
            Assert.Equal("2020-01-01", days[1].GetProperty("date").GetString());
            Assert.Equal(33.3, days[1].GetProperty("completionPct").GetDouble());
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }
}
