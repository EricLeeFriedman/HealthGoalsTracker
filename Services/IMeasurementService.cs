using System.Collections.Generic;
using System.Threading.Tasks;
using HealthGoalsTracker.Models;

namespace HealthGoalsTracker.Services;

public interface IMeasurementService
{
    Task<List<BodyMeasurement>> GetMeasurementsAsync();
    Task<bool> SaveMeasurementAsync(BodyMeasurement measurement);
}