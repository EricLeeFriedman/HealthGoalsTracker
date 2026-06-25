using SQLite;

namespace HealthGoalsTracker.Models;

// One row per goal per day. Snapshots the goal's name, emoji, and points at the time the day was
// created so that history remains accurate even if the user later edits or deletes a goal.
public class DailyGoalEntry
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string DailyRecordId { get; set; } = string.Empty;

    [Indexed]
    public string GoalId { get; set; } = string.Empty;

    // Snapshots of the goal's display properties for this specific day.
    public string GoalName { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = "⭐";
    public int GoalPoints { get; set; }
    public bool IsWeeklyOnly { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
