using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Services;

public interface IMeasurementService
{
    Task InitializeAsync();
    Task<List<BodyMeasurement>> GetMeasurementsAsync();
    Task<BodyMeasurement?> GetMeasurementForDateAsync(DateOnly date);
    Task SaveMeasurementAsync(BodyMeasurement measurement);
}
