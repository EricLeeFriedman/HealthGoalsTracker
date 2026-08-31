namespace HealthGoalsTracker.Functions.Contracts;

public class GoalContract
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string IconEmoji { get; set; } = "";
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public bool IsWeeklyOnly { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class DailyGoalEntryContract
{
    public string Id { get; set; } = "";
    public string GoalId { get; set; } = "";
    public string GoalName { get; set; } = "";
    public string IconEmoji { get; set; } = "";
    public int GoalPoints { get; set; }
    public bool IsWeeklyOnly { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class DailyRecordContract
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public int TotalPointsEarned { get; set; }
    public int TotalPointsPossible { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<DailyGoalEntryContract> Entries { get; set; } = [];
}

public class MeasurementContract
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public double? WeightLbs { get; set; }
    public double? BodyFatPercent { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class SyncRequest
{
    public string DeviceId { get; set; } = "";
    public string? Cursor { get; set; }
    public List<GoalContract> Goals { get; set; } = [];
    public List<DailyRecordContract> DailyRecords { get; set; } = [];
    public List<MeasurementContract> Measurements { get; set; } = [];
}

public class SyncResponse
{
    public DateTimeOffset ServerTime { get; set; }
    public string Cursor { get; set; } = "";
    public List<GoalContract> Goals { get; set; } = [];
    public List<DailyRecordContract> DailyRecords { get; set; } = [];
    public List<MeasurementContract> Measurements { get; set; } = [];
}

public class ApiError
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public Dictionary<string, string[]>? Details { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public string Version { get; set; } = "v1";
    public DateTimeOffset ServerTime { get; set; }
}
