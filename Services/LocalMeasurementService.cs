using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthGoalsTracker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        return await Database.Table<BodyMeasurement>()
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }

    public async Task<bool> SaveMeasurementAsync(BodyMeasurement measurement)
    {
        await InitializeAsync();
        measurement.UpdatedAt = DateTime.UtcNow;
        var existing = await Database.Table<BodyMeasurement>()
            .Where(item => item.UserId == measurement.UserId && item.Date == measurement.Date)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            measurement.Id = existing.Id;
            await Database.UpdateAsync(measurement);
            Logger.LogInformation("Existing measurement updated");
            return true;
        }

        await Database.InsertAsync(measurement);
        Logger.LogInformation("New measurement saved");
        return true;
    }
}