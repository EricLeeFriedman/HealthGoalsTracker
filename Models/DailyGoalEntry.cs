using SQLite;

namespace HealthGoalsTracker.Models;

// One row per goal per day. Snapshots the goal's name and points at the time the day was created
// so that history remains accurate even if the user later renames or re-points a goal.
public class DailyGoalEntry
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string DailyRecordId { get; set; } = string.Empty;

    [Indexed]
    public string GoalId { get; set; } = string.Empty;

    // Snapshot of the goal's display name and point value for this specific day.
    public string GoalName { get; set; } = string.Empty;
    public int GoalPoints { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
