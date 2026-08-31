using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.Tests;

public class LocalMeasurementServiceTests
{
    [Fact]
    public async Task SaveMeasurementAsync_UpdatesExistingMeasurementForSameUserAndDate()
    {
        var databasePath = DatabaseTestSupport.CreatePath("measurements");
        var service = new LocalMeasurementService(databasePath);

        try
        {
            var original = new BodyMeasurement
            {
                UserId = "local",
                Date = "2026-08-31",
                WeightLbs = 180
            };
            await service.SaveMeasurementAsync(original);

            var replacement = new BodyMeasurement
            {
                UserId = "local",
                Date = "2026-08-31",
                WeightLbs = 179,
                BodyFatPercent = 20
            };
            await service.SaveMeasurementAsync(replacement);

            var measurements = await service.GetMeasurementsAsync();

            var saved = Assert.Single(measurements);
            Assert.Equal(original.Id, saved.Id);
            Assert.Equal(179, saved.WeightLbs);
            Assert.Equal(20, saved.BodyFatPercent);
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }

    [Fact]
    public async Task GetMeasurementsAsync_ReturnsNewestDateFirst()
    {
        var databasePath = DatabaseTestSupport.CreatePath("measurements");
        var service = new LocalMeasurementService(databasePath);

        try
        {
            await service.SaveMeasurementAsync(new BodyMeasurement
            {
                Date = "2026-08-01",
                WeightLbs = 181
            });
            await service.SaveMeasurementAsync(new BodyMeasurement
            {
                Date = "2026-08-31",
                WeightLbs = 179
            });

            var measurements = await service.GetMeasurementsAsync();

            Assert.Equal(["2026-08-31", "2026-08-01"], measurements.Select(item => item.Date));
        }
        finally
        {
            await DatabaseTestSupport.DisposeAsync(service, databasePath);
        }
    }
}
