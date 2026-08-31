using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public static class DatabaseTestSupport
{
    public static string CreatePath(string scope) =>
        Path.Combine(Path.GetTempPath(), $"health-goals-{scope}-{Guid.NewGuid():N}.db3");

    public static async Task DisposeAsync(LocalGoalService service, string databasePath)
    {
        await service.Database.CloseAsync();
        File.Delete(databasePath);
    }

    public static async Task DisposeAsync(LocalMeasurementService service, string databasePath)
    {
        await service.Database.CloseAsync();
        File.Delete(databasePath);
    }
}
