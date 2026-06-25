using SQLite;

namespace HealthGoalsTracker.Models;

public class Goal
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Indexed]
    public string UserId { get; set; } = "local";

    public string Name { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = "⭐";
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsWeeklyOnly { get; set; }  // true = counts toward weekly score only (e.g. Strength Training)
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
