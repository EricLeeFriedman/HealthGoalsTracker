using SQLite;

namespace HealthGoalsTracker.Models;

public class UserSettings
{
    [PrimaryKey]
    public string UserId { get; set; } = "local";

    public bool NotificationsEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
