using SQLite;
using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Services;

public class LocalGoalService : IGoalService
{
    public SQLiteAsyncConnection Database;
    public SemaphoreSlim InitLock = new(1, 1);
    public bool IsInitialized;

    public LocalGoalService(string dbPath)
    {
        SQLitePCL.Batteries_V2.Init();
        Database = new SQLiteAsyncConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await InitLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            await Database.CreateTableAsync<Goal>();
            await Database.CreateTableAsync<DailyRecord>();
            await Database.CreateTableAsync<DailyGoalEntry>();
            await Database.CreateTableAsync<UserSettings>();
            await Database.CreateTableAsync<NotificationSchedule>();

            // Composite unique index: one DailyRecord per user per day.
            await Database.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_DailyRecord_UserDate ON DailyRecord (UserId, Date)");

            await SeedDefaultsAsync();
            IsInitialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    // -------------------------------------------------------------------------
    // Goals
    // -------------------------------------------------------------------------

    public async Task<List<Goal>> GetGoalsAsync()
    {
        await InitializeAsync();
        return await Database.Table<Goal>()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.SortOrder)
            .ToListAsync();
    }

    public async Task SaveGoalAsync(Goal goal)
    {
        await InitializeAsync();
        goal.UpdatedAt = DateTime.UtcNow;
        var existing = await Database.FindAsync<Goal>(goal.Id);
        if (existing == null)
            await Database.InsertAsync(goal);
        else
            await Database.UpdateAsync(goal);
    }

    public async Task DeleteGoalAsync(string goalId)
    {
        await InitializeAsync();
        var goal = await Database.FindAsync<Goal>(goalId);
        if (goal == null) return;

        goal.IsDeleted = true;
        goal.DeletedAt = DateTime.UtcNow;
        goal.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(goal);
    }

    public async Task ReorderGoalsAsync(List<Goal> goals)
    {
        await InitializeAsync();
        var now = DateTime.UtcNow;
        for (int i = 0; i < goals.Count; i++)
        {
            goals[i].SortOrder = i;
            goals[i].UpdatedAt = now;
        }
        await Database.UpdateAllAsync(goals);
    }

    // -------------------------------------------------------------------------
    // Daily records
    // -------------------------------------------------------------------------

    public async Task<DailyRecord> GetTodayRecordAsync()
    {
        await InitializeAsync();
        var todayKey = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var goals = await GetGoalsAsync();

        var record = await Database.Table<DailyRecord>()
            .Where(r => r.UserId == "local" && r.Date == todayKey)
            .FirstOrDefaultAsync();

        if (record == null)
        {
            // First access today — create the record and a snapshot entry for every active goal.
            record = new DailyRecord
            {
                Date = todayKey,
                TotalPointsPossible = goals.Sum(g => g.Points)
            };
            await Database.InsertAsync(record);

            var entries = goals.Select(g => new DailyGoalEntry
            {
                DailyRecordId = record.Id,
                GoalId = g.Id,
                GoalName = g.Name,
                GoalPoints = g.Points
            }).ToList();

            await Database.InsertAllAsync(entries);
            return record;
        }

        // Record already exists — reconcile any goals added since the record was created
        // (e.g. user adds a new goal mid-day, or goals arrive from cloud sync).
        var existingEntries = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id)
            .ToListAsync();
        var existingGoalIds = existingEntries.Select(e => e.GoalId).ToHashSet();

        var missingGoals = goals.Where(g => !existingGoalIds.Contains(g.Id)).ToList();
        if (missingGoals.Count > 0)
        {
            var newEntries = missingGoals.Select(g => new DailyGoalEntry
            {
                DailyRecordId = record.Id,
                GoalId = g.Id,
                GoalName = g.Name,
                GoalPoints = g.Points
            }).ToList();
            await Database.InsertAllAsync(newEntries);

            record.TotalPointsPossible = existingEntries.Sum(e => e.GoalPoints) + missingGoals.Sum(g => g.Points);
            record.UpdatedAt = DateTime.UtcNow;
            await Database.UpdateAsync(record);
        }

        return record;
    }

    public async Task<List<DailyGoalEntry>> GetDailyEntriesAsync(string dailyRecordId)
    {
        await InitializeAsync();
        return await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == dailyRecordId)
            .ToListAsync();
    }

    public async Task<DailyRecord?> GetRecordForDateAsync(DateOnly date)
    {
        await InitializeAsync();
        var dateKey = date.ToString("yyyy-MM-dd");
        return await Database.Table<DailyRecord>()
            .Where(r => r.UserId == "local" && r.Date == dateKey)
            .FirstOrDefaultAsync();
    }

    public async Task<List<DailyRecord>> GetRecordsForRangeAsync(DateOnly from, DateOnly to)
    {
        await InitializeAsync();
        var fromKey = from.ToString("yyyy-MM-dd");
        var toKey = to.ToString("yyyy-MM-dd");
        return await Database.QueryAsync<DailyRecord>(
            "SELECT * FROM DailyRecord WHERE UserId = 'local' AND Date >= ? AND Date <= ? ORDER BY Date",
            fromKey, toKey);
    }

    public async Task ToggleGoalCompletionAsync(string goalId)
    {
        await InitializeAsync();
        var record = await GetTodayRecordAsync();

        var entry = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id && e.GoalId == goalId)
            .FirstOrDefaultAsync();

        if (entry == null) return;

        entry.IsCompleted = !entry.IsCompleted;
        entry.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(entry);

        // Recalculate cached sums on the parent record.
        var allEntries = await GetDailyEntriesAsync(record.Id);
        record.TotalPointsEarned = allEntries.Where(e => e.IsCompleted).Sum(e => e.GoalPoints);
        record.TotalPointsPossible = allEntries.Sum(e => e.GoalPoints);
        record.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(record);
    }

    // -------------------------------------------------------------------------
    // Settings
    // -------------------------------------------------------------------------

    public async Task<UserSettings> GetUserSettingsAsync()
    {
        await InitializeAsync();
        return await Database.FindAsync<UserSettings>("local") ?? new UserSettings();
    }

    public async Task SaveUserSettingsAsync(UserSettings settings)
    {
        await InitializeAsync();
        settings.UpdatedAt = DateTime.UtcNow;
        var existing = await Database.FindAsync<UserSettings>(settings.UserId);
        if (existing == null)
            await Database.InsertAsync(settings);
        else
            await Database.UpdateAsync(settings);
    }

    public async Task<List<NotificationSchedule>> GetNotificationSchedulesAsync()
    {
        await InitializeAsync();
        return await Database.Table<NotificationSchedule>()
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task SaveNotificationScheduleAsync(NotificationSchedule schedule)
    {
        await InitializeAsync();
        var existing = await Database.FindAsync<NotificationSchedule>(schedule.Id);
        if (existing == null)
            await Database.InsertAsync(schedule);
        else
            await Database.UpdateAsync(schedule);
    }

    // -------------------------------------------------------------------------
    // Auth migration (Phase 8)
    // -------------------------------------------------------------------------

    // Called once after first sign-in to claim all "local" data for the authenticated user.
    public async Task UpdateUserIdAsync(string newUserId)
    {
        await InitializeAsync();
        await Database.ExecuteAsync(
            "UPDATE Goal SET UserId = ? WHERE UserId = 'local'", newUserId);
        await Database.ExecuteAsync(
            "UPDATE DailyRecord SET UserId = ? WHERE UserId = 'local'", newUserId);
        await Database.ExecuteAsync(
            "UPDATE UserSettings SET UserId = ? WHERE UserId = 'local'", newUserId);
    }

    // -------------------------------------------------------------------------
    // Today snapshot helpers (called when a goal is edited or deleted mid-day)
    // -------------------------------------------------------------------------

    // Updates today's DailyGoalEntry snapshot when the user renames or re-points a goal.
    // Recalculates DailyRecord totals if the point value changed.
    public async Task UpdateTodayGoalSnapshotAsync(string goalId, string newName, int newPoints)
    {
        await InitializeAsync();
        var todayKey = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        var record = await Database.Table<DailyRecord>()
            .Where(r => r.UserId == "local" && r.Date == todayKey)
            .FirstOrDefaultAsync();
        if (record == null) return;

        var entry = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id && e.GoalId == goalId)
            .FirstOrDefaultAsync();
        if (entry == null) return;

        bool pointsChanged = entry.GoalPoints != newPoints;
        entry.GoalName = newName;
        entry.GoalPoints = newPoints;
        entry.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(entry);

        if (pointsChanged)
        {
            var allEntries = await Database.Table<DailyGoalEntry>()
                .Where(e => e.DailyRecordId == record.Id)
                .ToListAsync();
            record.TotalPointsPossible = allEntries.Sum(e => e.GoalPoints);
            record.TotalPointsEarned   = allEntries.Where(e => e.IsCompleted).Sum(e => e.GoalPoints);
            record.UpdatedAt = DateTime.UtcNow;
            await Database.UpdateAsync(record);
        }
    }

    // Removes today's DailyGoalEntry when a goal is deleted mid-day,
    // so the deleted goal no longer contributes to today's possible points.
    public async Task RemoveTodayGoalEntryAsync(string goalId)
    {
        await InitializeAsync();
        var todayKey = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        var record = await Database.Table<DailyRecord>()
            .Where(r => r.UserId == "local" && r.Date == todayKey)
            .FirstOrDefaultAsync();
        if (record == null) return;

        var entry = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id && e.GoalId == goalId)
            .FirstOrDefaultAsync();
        if (entry == null) return;

        await Database.DeleteAsync(entry);

        var allEntries = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id)
            .ToListAsync();
        record.TotalPointsPossible = allEntries.Sum(e => e.GoalPoints);
        record.TotalPointsEarned   = allEntries.Where(e => e.IsCompleted).Sum(e => e.GoalPoints);
        record.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(record);
    }

    public async Task ResetTodayAsync()
    {
        await InitializeAsync();
        var todayKey = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        var record = await Database.Table<DailyRecord>()
            .Where(r => r.UserId == "local" && r.Date == todayKey)
            .FirstOrDefaultAsync();
        if (record == null) return;

        var entries = await Database.Table<DailyGoalEntry>()
            .Where(e => e.DailyRecordId == record.Id)
            .ToListAsync();

        foreach (var entry in entries)
        {
            entry.IsCompleted = false;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await Database.UpdateAllAsync(entries);

        record.TotalPointsEarned = 0;
        record.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(record);
    }

    // -------------------------------------------------------------------------
    // Seeding
    // -------------------------------------------------------------------------

    async Task SeedDefaultsAsync()
    {
        var goalCount = await Database.Table<Goal>().CountAsync();
        if (goalCount == 0)
        {
            var defaults = new List<Goal>
            {
                new() { Name = "Slept at least 7 hours",         Points = 3, SortOrder = 0, IsDefault = true },
                new() { Name = "Ate less than 2200 Calories",    Points = 3, SortOrder = 1, IsDefault = true },
                new() { Name = "Fasted for at least 12 hours",   Points = 2, SortOrder = 2, IsDefault = true },
                new() { Name = "Drank at least 70oz of water",   Points = 2, SortOrder = 3, IsDefault = true },
                new() { Name = "Ate at least 150g of Protein",   Points = 2, SortOrder = 4, IsDefault = true },
                new() { Name = "Meditated for at least 5 min",   Points = 1, SortOrder = 5, IsDefault = true },
            };
            await Database.InsertAllAsync(defaults);
        }

        var notifCount = await Database.Table<NotificationSchedule>().CountAsync();
        if (notifCount == 0)
        {
            var defaultSchedules = new List<NotificationSchedule>
            {
                new() { Type = NotificationType.NudgeIfNoGoalsCompleted, HourOfDay = 12, MinuteOfHour = 0, SortOrder = 0 },
                new() { Type = NotificationType.NudgeIfNoGoalsCompleted, HourOfDay = 16, MinuteOfHour = 0, SortOrder = 1 },
                new() { Type = NotificationType.DailySummary,            HourOfDay = 21, MinuteOfHour = 0, SortOrder = 2 },
                new() { Type = NotificationType.MorningRecap,            HourOfDay = 7,  MinuteOfHour = 0, SortOrder = 3 },
            };
            await Database.InsertAllAsync(defaultSchedules);
        }

        var settings = await Database.FindAsync<UserSettings>("local");
        if (settings == null)
            await Database.InsertAsync(new UserSettings());
    }
}
