using System.Collections.Concurrent;
using System.Text.Json;
using HealthGoalsTracker.Functions.Contracts;

namespace HealthGoalsTracker.Functions.Services;

public class InMemoryCloudRepository : ICloudRepository
{
    public ConcurrentDictionary<string, UserPartition> Partitions { get; } = new();
    public CursorCodec CursorCodec { get; }

    public InMemoryCloudRepository(CursorCodec cursorCodec)
    {
        CursorCodec = cursorCodec;
    }

    public Task<SyncResponse> SyncAsync(
        string subject,
        SyncRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CursorCodec.TryDecode(subject, request.Cursor, out var afterSequence);
        var partition = Partitions.GetOrAdd(subject, _ => new UserPartition());

        lock (partition.SyncRoot)
        {
            if (afterSequence > partition.Sequence)
                throw new InvalidCursorException();

            foreach (var goal in request.Goals)
                Upsert(partition, "goal", goal.Id, goal.UpdatedAt, Clone(goal));
            foreach (var record in request.DailyRecords)
            {
                var recalculated = Clone(record);
                recalculated.TotalPointsEarned = recalculated.Entries
                    .Where(entry => entry.IsCompleted && !entry.IsWeeklyOnly)
                    .Sum(entry => entry.GoalPoints);
                recalculated.TotalPointsPossible = recalculated.Entries
                    .Where(entry => !entry.IsWeeklyOnly)
                    .Sum(entry => entry.GoalPoints);
                Upsert(
                    partition,
                    "record",
                    recalculated.Id,
                    recalculated.UpdatedAt,
                    recalculated);
            }
            foreach (var measurement in request.Measurements)
                Upsert(
                    partition,
                    "measurement",
                    measurement.Id,
                    measurement.UpdatedAt,
                    Clone(measurement));

            var returnedKeys = partition.Changes
                .Where(change => change.Sequence > afterSequence)
                .Select(change => (change.Kind, change.Id))
                .ToHashSet();
            returnedKeys.UnionWith(request.Goals.Select(goal => ("goal", goal.Id)));
            returnedKeys.UnionWith(request.DailyRecords.Select(record => ("record", record.Id)));
            returnedKeys.UnionWith(request.Measurements.Select(measurement => ("measurement", measurement.Id)));

            return Task.FromResult(new SyncResponse
            {
                ServerTime = DateTimeOffset.UtcNow,
                Cursor = CursorCodec.Encode(subject, partition.Sequence),
                Goals = partition.Goals.Values
                    .Where(goal => returnedKeys.Contains(("goal", goal.Id)))
                    .OrderBy(goal => goal.SortOrder)
                    .ThenBy(goal => goal.Id, StringComparer.Ordinal)
                    .Select(Clone)
                    .ToList(),
                DailyRecords = partition.Records.Values
                    .Where(record => returnedKeys.Contains(("record", record.Id)))
                    .OrderBy(record => record.Date, StringComparer.Ordinal)
                    .ThenBy(record => record.Id, StringComparer.Ordinal)
                    .Select(Clone)
                    .ToList(),
                Measurements = partition.Measurements.Values
                    .Where(measurement => returnedKeys.Contains(("measurement", measurement.Id)))
                    .OrderBy(measurement => measurement.Date, StringComparer.Ordinal)
                    .ThenBy(measurement => measurement.Id, StringComparer.Ordinal)
                    .Select(Clone)
                    .ToList()
            });
        }

    }

    public Task<List<GoalContract>> GetGoalsAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var partition = Partitions.GetOrAdd(subject, _ => new UserPartition());
        lock (partition.SyncRoot)
        {
            return Task.FromResult(partition.Goals.Values
                .OrderBy(goal => goal.SortOrder)
                .ThenBy(goal => goal.Id, StringComparer.Ordinal)
                .Select(Clone)
                .ToList());
        }
    }

    public Task<List<DailyRecordContract>> GetRecordsAsync(
        string subject,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var partition = Partitions.GetOrAdd(subject, _ => new UserPartition());
        lock (partition.SyncRoot)
        {
            return Task.FromResult(partition.Records.Values
                .Where(record =>
                    ContractValidator.TryParseDate(record.Date, out var date) &&
                    date >= from &&
                    date <= to)
                .OrderBy(record => record.Date, StringComparer.Ordinal)
                .ThenBy(record => record.Id, StringComparer.Ordinal)
                .Select(Clone)
                .ToList());
        }
    }

    public Task<List<MeasurementContract>> GetMeasurementsAsync(
        string subject,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var partition = Partitions.GetOrAdd(subject, _ => new UserPartition());
        lock (partition.SyncRoot)
        {
            return Task.FromResult(partition.Measurements.Values
                .Where(measurement =>
                    ContractValidator.TryParseDate(measurement.Date, out var date) &&
                    date >= from &&
                    date <= to)
                .OrderBy(measurement => measurement.Date, StringComparer.Ordinal)
                .ThenBy(measurement => measurement.Id, StringComparer.Ordinal)
                .Select(Clone)
                .ToList());
        }
    }

    public static void Upsert<T>(
        UserPartition partition,
        string kind,
        string id,
        DateTimeOffset updatedAt,
        T value)
        where T : class
    {
        var dictionary = kind switch
        {
            "goal" => (object)partition.Goals,
            "record" => partition.Records,
            "measurement" => partition.Measurements,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var currentTimestamp = dictionary switch
        {
            Dictionary<string, GoalContract> goals when goals.TryGetValue(id, out var goal) =>
                goal.UpdatedAt,
            Dictionary<string, DailyRecordContract> records when records.TryGetValue(id, out var record) =>
                record.UpdatedAt,
            Dictionary<string, MeasurementContract> measurements when measurements.TryGetValue(id, out var measurement) =>
                measurement.UpdatedAt,
            _ => (DateTimeOffset?)null
        };
        if (currentTimestamp.HasValue)
        {
            if (currentTimestamp.Value > updatedAt)
                return;
            if (currentTimestamp.Value == updatedAt)
            {
                var currentCanonical = dictionary switch
                {
                    Dictionary<string, GoalContract> goals => JsonSerializer.Serialize(goals[id]),
                    Dictionary<string, DailyRecordContract> records => JsonSerializer.Serialize(records[id]),
                    Dictionary<string, MeasurementContract> measurements => JsonSerializer.Serialize(measurements[id]),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };
                var candidateCanonical = JsonSerializer.Serialize(value);
                if (string.CompareOrdinal(currentCanonical, candidateCanonical) >= 0)
                    return;
            }
        }

        switch (dictionary)
        {
            case Dictionary<string, GoalContract> goals:
                goals[id] = (GoalContract)(object)value;
                break;
            case Dictionary<string, DailyRecordContract> records:
                records[id] = (DailyRecordContract)(object)value;
                break;
            case Dictionary<string, MeasurementContract> measurements:
                measurements[id] = (MeasurementContract)(object)value;
                break;
        }
        partition.Sequence++;
        partition.Changes.Add(new ChangeEntry(partition.Sequence, kind, id));
    }

    public static GoalContract Clone(GoalContract value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        IconEmoji = value.IconEmoji,
        Points = value.Points,
        SortOrder = value.SortOrder,
        IsDefault = value.IsDefault,
        IsWeeklyOnly = value.IsWeeklyOnly,
        IsDeleted = value.IsDeleted,
        UpdatedAt = value.UpdatedAt,
        DeletedAt = value.DeletedAt
    };

    public static DailyGoalEntryContract Clone(DailyGoalEntryContract value) => new()
    {
        Id = value.Id,
        GoalId = value.GoalId,
        GoalName = value.GoalName,
        IconEmoji = value.IconEmoji,
        GoalPoints = value.GoalPoints,
        IsWeeklyOnly = value.IsWeeklyOnly,
        IsCompleted = value.IsCompleted,
        UpdatedAt = value.UpdatedAt
    };

    public static DailyRecordContract Clone(DailyRecordContract value) => new()
    {
        Id = value.Id,
        Date = value.Date,
        TotalPointsEarned = value.TotalPointsEarned,
        TotalPointsPossible = value.TotalPointsPossible,
        UpdatedAt = value.UpdatedAt,
        Entries = value.Entries.Select(Clone).ToList()
    };

    public static MeasurementContract Clone(MeasurementContract value) => new()
    {
        Id = value.Id,
        Date = value.Date,
        WeightLbs = value.WeightLbs,
        BodyFatPercent = value.BodyFatPercent,
        Notes = value.Notes,
        UpdatedAt = value.UpdatedAt
    };
}

public class UserPartition
{
    public object SyncRoot { get; } = new();
    public Dictionary<string, GoalContract> Goals { get; } = [];
    public Dictionary<string, DailyRecordContract> Records { get; } = [];
    public Dictionary<string, MeasurementContract> Measurements { get; } = [];
    public List<ChangeEntry> Changes { get; } = [];
    public long Sequence { get; set; }
}

public record ChangeEntry(long Sequence, string Kind, string Id);

public class InvalidCursorException : Exception;
