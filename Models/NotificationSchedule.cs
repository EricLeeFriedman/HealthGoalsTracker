using SQLite;

namespace HealthGoalsTracker.Models;

public class NotificationSchedule
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public NotificationType Type { get; set; }

    // Stored as separate int fields since sqlite-net-pcl does not support TimeOnly natively.
    public int HourOfDay { get; set; }
    public int MinuteOfHour { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Controls display order within a NotificationType group.
    public int SortOrder { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public TimeOnly Time => new(HourOfDay, MinuteOfHour);

    // Convenience aliases used by NotificationScheduler
    [Ignore] public int Hour   { get => HourOfDay;   set => HourOfDay   = value; }
    [Ignore] public int Minute { get => MinuteOfHour; set => MinuteOfHour = value; }
}
