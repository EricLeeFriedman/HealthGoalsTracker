using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class MigrationTests
{
    [Fact]
    public async Task InitializeAsync_CorrectsDefaultMeditationNameWithoutRewritingUserSnapshots()
    {
        var databasePath = DatabaseTestSupport.CreatePath("migration");
        var setupService = new LocalGoalService(databasePath);
        LocalGoalService? migratedService = null;

        try
        {
            await setupService.InitializeAsync();
            var defaultGoal = Assert.Single(
                await setupService.GetGoalsAsync(),
                goal => goal.Name == "Meditated for at least 5 minutes");
            defaultGoal.Name = "Meditated for at least 5 min";
            await setupService.Database.UpdateAsync(defaultGoal);

            var customGoal = new Goal
            {
                Name = "Meditated for at least 5 min",
                IconEmoji = "⭐",
                Points = 2,
                SortOrder = 20,
                IsDefault = false
            };
            await setupService.Database.InsertAsync(customGoal);

            var record = new DailyRecord
            {
                UserId = "local",
                Date = "2026-08-01",
                TotalPointsPossible = 3
            };
            await setupService.Database.InsertAsync(record);
            await setupService.Database.InsertAllAsync(
                new[]
                {
                    CreateEntry(record.Id, defaultGoal),
                    CreateEntry(record.Id, customGoal)
                });
            await setupService.Database.CloseAsync();

            migratedService = new LocalGoalService(databasePath);
            await migratedService.InitializeAsync();

            var goals = await migratedService.Database.Table<Goal>().ToListAsync();
            Assert.Equal(
                "Meditated for at least 5 minutes",
                goals.Single(goal => goal.Id == defaultGoal.Id).Name);
            Assert.Equal(
                "Meditated for at least 5 min",
                goals.Single(goal => goal.Id == customGoal.Id).Name);

            var entries = await migratedService.GetDailyEntriesAsync(record.Id);
            Assert.Equal(
                "Meditated for at least 5 minutes",
                entries.Single(entry => entry.GoalId == defaultGoal.Id).GoalName);
            Assert.Equal(
                "Meditated for at least 5 min",
                entries.Single(entry => entry.GoalId == customGoal.Id).GoalName);
        }
        finally
        {
            if (migratedService != null)
                await DatabaseTestSupport.DisposeAsync(migratedService, databasePath);
            else
            {
                await setupService.Database.CloseAsync();
                File.Delete(databasePath);
            }
        }
    }

    public static DailyGoalEntry CreateEntry(string recordId, Goal goal) =>
        new()
        {
            DailyRecordId = recordId,
            GoalId = goal.Id,
            GoalName = goal.Name,
            IconEmoji = goal.IconEmoji,
            GoalPoints = goal.Points
        };
}
