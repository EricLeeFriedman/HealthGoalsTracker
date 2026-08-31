using System.Text.Json;

namespace HealthGoalsTracker.Services;

public static class DataExportService
{
    public static async Task<string> BuildJsonAsync(IGoalService goalService)
    {
        var records = await goalService.GetRecordsForRangeAsync(
            DateOnly.MinValue,
            DateOnly.MaxValue);
        var days = new List<object>();
        foreach (var record in records)
        {
            var entries = await goalService.GetDailyEntriesAsync(record.Id);
            days.Add(new
            {
                date = record.Date,
                pointsEarned = record.TotalPointsEarned,
                pointsPossible = record.TotalPointsPossible,
                completionPct = Math.Round(record.CompletionPercent, 1),
                goals = entries.Select(entry => new
                {
                    name = entry.GoalName,
                    iconEmoji = entry.IconEmoji,
                    points = entry.GoalPoints,
                    isWeeklyOnly = entry.IsWeeklyOnly,
                    completed = entry.IsCompleted
                })
            });
        }

        return JsonSerializer.Serialize(
            new { exportedAt = DateTime.UtcNow, days },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
