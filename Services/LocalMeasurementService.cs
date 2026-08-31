using HealthGoalsTracker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace HealthGoalsTracker.Services;

public class LocalMeasurementService : IMeasurementService
{
    public SQLiteAsyncConnection Database;
    public SemaphoreSlim InitLock = new(1, 1);
    public bool IsInitialized;
    public ILogger<LocalMeasurementService> Logger;

    public LocalMeasurementService(
        string dbPath,
        ILogger<LocalMeasurementService>? logger = null)
    {
        Logger = logger ?? NullLogger<LocalMeasurementService>.Instance;
        SQLitePCL.Batteries_V2.Init();
        Database = new SQLiteAsyncConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await InitLock.WaitAsync();
        try
        {
            if (IsInitialized) return;

            await Database.CreateTableAsync<BodyMeasurement>();
            await Database.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_BodyMeasurement_UserDate ON BodyMeasurement (UserId, Date)");

            IsInitialized = true;
            Logger.LogInformation("Measurement database initialized");
        }
        finally
        {
            InitLock.Release();
        }
    }

    public async Task<List<BodyMeasurement>> GetMeasurementsAsync()
    {
        await InitializeAsync();
        return await Database.QueryAsync<BodyMeasurement>(
            "SELECT * FROM BodyMeasurement WHERE UserId = 'local' ORDER BY Date DESC");
    }

    public async Task<BodyMeasurement?> GetMeasurementForDateAsync(DateOnly date)
    {
        await InitializeAsync();
        var dateKey = date.ToString("yyyy-MM-dd");
        return await Database.Table<BodyMeasurement>()
            .Where(m => m.UserId == "local" && m.Date == dateKey)
            .FirstOrDefaultAsync();
    }

    public async Task SaveMeasurementAsync(BodyMeasurement measurement)
    {
        await InitializeAsync();

        measurement.UserId = "local";
        measurement.UpdatedAt = DateTime.UtcNow;

        var existing = await Database.Table<BodyMeasurement>()
            .Where(m => m.UserId == measurement.UserId && m.Date == measurement.Date)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            await Database.InsertAsync(measurement);
            Logger.LogInformation("New measurement saved");
            return;
        }

        existing.WeightLbs = measurement.WeightLbs;
        existing.BodyFatPercent = measurement.BodyFatPercent;
        existing.Notes = measurement.Notes;
        existing.UpdatedAt = measurement.UpdatedAt;
        await Database.UpdateAsync(existing);
        Logger.LogInformation("Existing measurement updated");
    }
}
