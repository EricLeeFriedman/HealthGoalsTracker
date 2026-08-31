using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Tests;

public static class FeatureTestAssertions
{
    public static void AssertGoal(
        Goal goal,
        string name,
        string iconEmoji,
        int points,
        bool isWeeklyOnly)
    {
        Assert.Equal(name, goal.Name);
        Assert.Equal(iconEmoji, goal.IconEmoji);
        Assert.Equal(points, goal.Points);
        Assert.Equal(isWeeklyOnly, goal.IsWeeklyOnly);
        Assert.True(goal.IsDefault);
        Assert.False(goal.IsDeleted);
    }

    public static void AssertSchedule(
        NotificationSchedule schedule,
        NotificationType type,
        int hour,
        int minute)
    {
        Assert.Equal(type, schedule.Type);
        Assert.Equal(new TimeOnly(hour, minute), schedule.Time);
        Assert.True(schedule.IsEnabled);
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
