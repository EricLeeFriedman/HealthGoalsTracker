using HealthGoalsTracker.Functions.Contracts;

namespace HealthGoalsTracker.Functions.Services;

public interface ICloudRepository
{
    public Task<SyncResponse> SyncAsync(
        string subject,
        SyncRequest request,
        CancellationToken cancellationToken);

    public Task<List<GoalContract>> GetGoalsAsync(
        string subject,
        CancellationToken cancellationToken);

    public Task<List<DailyRecordContract>> GetRecordsAsync(
        string subject,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    public Task<List<MeasurementContract>> GetMeasurementsAsync(
        string subject,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
