using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class GoalPersistenceTests
{
    [Fact]
    public async Task GetGoalsAsync_SeedsDocumentedDefaultsInDisplayOrder()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var goals = await service.GetGoalsAsync();

            Assert.Collection(
                goals,
                goal => FeatureTestAssertions.AssertGoal(goal, "Slept at least 7 hours", "😴", 3, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Ate less than 2200 Calories", "🍽️", 3, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Ate at least 150g of Protein", "🥩", 3, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Movement", "🏃", 2, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Drank at least 70oz of water", "💧", 1, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Meditated for at least 5 minutes", "🧘", 1, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Fasted for at least 12 hours", "⏱️", 1, false),
                goal => FeatureTestAssertions.AssertGoal(goal, "Strength Training", "💪", 0, true));
            Assert.Equal(Enumerable.Range(0, 8), goals.Select(goal => goal.SortOrder));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetTodayRecordAsync_ReusesOneRecordAndSnapshotsEveryActiveGoal()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var first = await service.GetTodayRecordAsync();
            var second = await service.GetTodayRecordAsync();
            var goals = await service.GetGoalsAsync();
            var entries = await service.GetDailyEntriesAsync(first.Id);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), first.Date);
            Assert.Equal(goals.Count, entries.Count);
            var entriesByGoalId = entries.ToDictionary(entry => entry.GoalId);
            foreach (var goal in goals)
            {
                var entry = entriesByGoalId[goal.Id];
                Assert.Equal(goal.Name, entry.GoalName);
                Assert.Equal(goal.IconEmoji, entry.IconEmoji);
                Assert.Equal(goal.Points, entry.GoalPoints);
                Assert.Equal(goal.IsWeeklyOnly, entry.IsWeeklyOnly);
            }
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetTodayRecordAsync_ConcurrentFirstAccessCreatesOneRecord()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var records = await Task.WhenAll(
                Enumerable.Range(0, 20)
                    .Select(_ => service.GetTodayRecordAsync()));

            Assert.Single(records.Select(record => record.Id).Distinct());
            Assert.Single(await service.Database.Table<DailyRecord>().ToListAsync());
            Assert.Equal(
                8,
                (await service.Database.Table<DailyGoalEntry>().ToListAsync()).Count);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task SaveGoalAsync_AddsAndUpdatesEditableGoalFields()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var goal = new Goal
            {
                Name = "Stretch",
                IconEmoji = "🤸",
                Points = 2,
                SortOrder = 8,
                IsWeeklyOnly = false
            };

            await service.SaveGoalAsync(goal);
            goal.Name = "Mobility";
            goal.IconEmoji = "🧎";
            goal.Points = 1;
            goal.IsWeeklyOnly = true;
            await service.SaveGoalAsync(goal);

            var saved = Assert.Single(await service.GetGoalsAsync(), item => item.Id == goal.Id);
            Assert.Equal("Mobility", saved.Name);
            Assert.Equal("🧎", saved.IconEmoji);
            Assert.Equal(1, saved.Points);
            Assert.True(saved.IsWeeklyOnly);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task DeleteGoalAsync_SoftDeletesGoalAndExcludesItFromActiveGoals()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var goal = (await service.GetGoalsAsync())[0];

            await service.DeleteGoalAsync(goal.Id);

            Assert.DoesNotContain(await service.GetGoalsAsync(), item => item.Id == goal.Id);
            var stored = await service.Database.Table<Goal>()
                .Where(item => item.Id == goal.Id)
                .FirstAsync();
            Assert.True(stored.IsDeleted);
            Assert.NotNull(stored.DeletedAt);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task ReorderGoalsAsync_PersistsRequestedDisplayOrder()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var goals = await service.GetGoalsAsync();
            goals.Reverse();

            await service.ReorderGoalsAsync(goals);

            Assert.Equal(goals.Select(goal => goal.Id), (await service.GetGoalsAsync()).Select(goal => goal.Id));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task ToggleGoalCompletionAsync_SecondTapReturnsGoalToIncomplete()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var goal = Assert.Single(await service.GetGoalsAsync(), item => item.Name == "Movement");

            await service.ToggleGoalCompletionAsync(goal.Id);
            await service.ToggleGoalCompletionAsync(goal.Id);

            var entry = Assert.Single(
                await service.GetDailyEntriesAsync(record.Id),
                item => item.GoalId == goal.Id);
            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.False(entry.IsCompleted);
            Assert.NotNull(updated);
            Assert.Equal(0, updated.TotalPointsEarned);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task UpdateTodayGoalSnapshotAsync_UpdatesDisplayDataAndRecalculatesDailyTotals()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var goal = Assert.Single(await service.GetGoalsAsync(), item => item.Name == "Movement");
            await service.ToggleGoalCompletionAsync(goal.Id);

            await service.UpdateTodayGoalSnapshotAsync(
                goal.Id,
                "Outdoor Movement",
                5,
                "🚶",
                false);

            var entry = Assert.Single(
                await service.GetDailyEntriesAsync(record.Id),
                item => item.GoalId == goal.Id);
            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.Equal("Outdoor Movement", entry.GoalName);
            Assert.Equal("🚶", entry.IconEmoji);
            Assert.Equal(5, entry.GoalPoints);
            Assert.False(entry.IsWeeklyOnly);
            Assert.NotNull(updated);
            Assert.Equal(5, updated.TotalPointsEarned);
            Assert.Equal(17, updated.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task RemoveTodayGoalEntryAsync_RemovesGoalAndRecalculatesDailyTotals()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var goal = Assert.Single(await service.GetGoalsAsync(), item => item.Name == "Movement");
            await service.ToggleGoalCompletionAsync(goal.Id);

            await service.RemoveTodayGoalEntryAsync(goal.Id);

            Assert.DoesNotContain(
                await service.GetDailyEntriesAsync(record.Id),
                item => item.GoalId == goal.Id);
            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.NotNull(updated);
            Assert.Equal(0, updated.TotalPointsEarned);
            Assert.Equal(12, updated.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task UpdateTodayGoalSnapshotAsync_DoesNotRewriteHistoricalSnapshots()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.InitializeAsync();
            var goal = (await service.GetGoalsAsync())[0];
            var historicalRecord = new DailyRecord
            {
                UserId = "local",
                Date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1).ToString("yyyy-MM-dd"),
                TotalPointsPossible = 14
            };
            var historicalEntry = new DailyGoalEntry
            {
                DailyRecordId = historicalRecord.Id,
                GoalId = goal.Id,
                GoalName = goal.Name,
                IconEmoji = goal.IconEmoji,
                GoalPoints = goal.Points
            };
            await service.Database.InsertAsync(historicalRecord);
            await service.Database.InsertAsync(historicalEntry);
            var today = await service.GetTodayRecordAsync();

            await service.UpdateTodayGoalSnapshotAsync(
                goal.Id,
                "Updated name",
                9,
                "✅",
                false);

            var oldEntry = Assert.Single(await service.GetDailyEntriesAsync(historicalRecord.Id));
            var todayEntry = Assert.Single(
                await service.GetDailyEntriesAsync(today.Id),
                entry => entry.GoalId == goal.Id);
            Assert.Equal(goal.Name, oldEntry.GoalName);
            Assert.Equal(goal.Points, oldEntry.GoalPoints);
            Assert.Equal("Updated name", todayEntry.GoalName);
            Assert.Equal(9, todayEntry.GoalPoints);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetTodayRecordAsync_AddsNewGoalsCreatedAfterTheDayStarted()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            var record = await service.GetTodayRecordAsync();
            var goal = new Goal
            {
                Name = "Stretch",
                IconEmoji = "🤸",
                Points = 2,
                SortOrder = 8
            };
            await service.SaveGoalAsync(goal);

            var updatedRecord = await service.GetTodayRecordAsync();
            var entries = await service.GetDailyEntriesAsync(record.Id);

            Assert.Equal(record.Id, updatedRecord.Id);
            Assert.Single(entries, entry => entry.GoalId == goal.Id);
            Assert.Equal(16, updatedRecord.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task UpdateTodayGoalSnapshotAsync_TogglingToWeeklyRemovesGoalFromDailyTotals()
    {
        var databasePath = DatabaseTestSupport.CreatePath("goals");
        var service = new LocalGoalService(databasePath);

        try
        {
            await service.GetTodayRecordAsync();
            var movement = Assert.Single(
                await service.GetGoalsAsync(),
                goal => goal.Name == "Movement");
            await service.ToggleGoalCompletionAsync(movement.Id);

            await service.UpdateTodayGoalSnapshotAsync(
                movement.Id,
                movement.Name,
                movement.Points,
                movement.IconEmoji,
                true);

            var updated = await service.GetRecordForDateAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.NotNull(updated);
            Assert.Equal(0, updated.TotalPointsEarned);
            Assert.Equal(12, updated.TotalPointsPossible);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }
}
