using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Services;

public interface IGoalService
{
    Task InitializeAsync();

    // Goals
    Task<List<Goal>> GetGoalsAsync();
    Task SaveGoalAsync(Goal goal);
    Task DeleteGoalAsync(string goalId);
    Task ReorderGoalsAsync(List<Goal> goals);

    // Daily records
    Task<DailyRecord> GetTodayRecordAsync();
    Task<List<DailyGoalEntry>> GetDailyEntriesAsync(string dailyRecordId);
    Task<DailyRecord?> GetRecordForDateAsync(DateOnly date);
    Task<List<DailyRecord>> GetRecordsForRangeAsync(DateOnly from, DateOnly to);
    Task ToggleGoalCompletionAsync(string goalId);

    // Settings
    Task<UserSettings> GetUserSettingsAsync();
    Task SaveUserSettingsAsync(UserSettings settings);
    Task<List<NotificationSchedule>> GetNotificationSchedulesAsync();
    Task SaveNotificationScheduleAsync(NotificationSchedule schedule);

    // Called once during Phase 8 (auth) to claim local data for the signed-in user.
    Task UpdateUserIdAsync(string newUserId);

    // Keeps today's DailyGoalEntry snapshot in sync when a goal is edited or deleted mid-day.
    Task UpdateTodayGoalSnapshotAsync(string goalId, string newName, int newPoints, string iconEmoji);
    Task RemoveTodayGoalEntryAsync(string goalId);

    // Clears all completions for today (Reset Today action in hamburger menu).
    Task ResetTodayAsync();
}
