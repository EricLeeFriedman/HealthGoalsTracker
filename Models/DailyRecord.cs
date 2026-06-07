using SQLite;

namespace HealthGoalsTracker.Models;

// One row per user per calendar day. Goal-level detail lives in DailyGoalEntry.
public class DailyRecord
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string UserId { get; set; } = "local";

    // Stored as "yyyy-MM-dd". ISO format lets SQLite string comparison do range queries correctly.
    [Indexed]
    public string Date { get; set; } = string.Empty;

    // Cached sums — recomputed whenever a DailyGoalEntry changes.
    public int TotalPointsEarned { get; set; }
    public int TotalPointsPossible { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public DateOnly RecordDate => DateOnly.Parse(Date);

    [Ignore]
    public double CompletionPercent =>
        TotalPointsPossible == 0 ? 0 : (double)TotalPointsEarned / TotalPointsPossible * 100.0;
}
