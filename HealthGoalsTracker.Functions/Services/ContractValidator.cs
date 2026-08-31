using System.Globalization;
using HealthGoalsTracker.Functions.Contracts;

namespace HealthGoalsTracker.Functions.Services;

public class ContractValidator
{
    public CursorCodec CursorCodec { get; }
    public const int MaxBatchSize = 100;
    public const int MaxGoalNameLength = 120;
    public const int MaxEmojiLength = 16;
    public const int MaxNotesLength = 1000;
    public const int MaxEntriesPerRecord = 100;

    public ContractValidator(CursorCodec cursorCodec)
    {
        CursorCodec = cursorCodec;
    }

    public Dictionary<string, string[]> ValidateSync(string subject, SyncRequest request)
    {
        var errors = new Dictionary<string, List<string>>();
        AddIf(
            errors,
            "deviceId",
            !Guid.TryParse(request.DeviceId, out _),
            "DeviceId must be a GUID.");
        AddIf(
            errors,
            "cursor",
            !CursorCodec.TryDecode(subject, request.Cursor, out _),
            "Cursor is invalid.");
        ValidateCount(errors, "goals", request.Goals.Count);
        ValidateCount(errors, "dailyRecords", request.DailyRecords.Count);
        ValidateCount(errors, "measurements", request.Measurements.Count);

        for (var index = 0; index < request.Goals.Count; index++)
            ValidateGoal(errors, $"goals[{index}]", request.Goals[index]);
        for (var index = 0; index < request.DailyRecords.Count; index++)
            ValidateRecord(errors, $"dailyRecords[{index}]", request.DailyRecords[index]);
        for (var index = 0; index < request.Measurements.Count; index++)
            ValidateMeasurement(errors, $"measurements[{index}]", request.Measurements[index]);

        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }

    public Dictionary<string, string[]> ValidateDateRange(string? from, string? to)
    {
        var errors = new Dictionary<string, List<string>>();
        var fromValid = TryParseDate(from, out var fromDate);
        var toValid = TryParseDate(to, out var toDate);
        AddIf(errors, "from", !fromValid, "From must use yyyy-MM-dd.");
        AddIf(errors, "to", !toValid, "To must use yyyy-MM-dd.");
        if (fromValid && toValid)
        {
            AddIf(errors, "range", fromDate > toDate, "From must not be after To.");
            AddIf(
                errors,
                "range",
                toDate.DayNumber - fromDate.DayNumber > 366,
                "Date range must not exceed 366 days.");
        }
        return errors.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }

    public void ValidateGoal(
        Dictionary<string, List<string>> errors,
        string path,
        GoalContract goal)
    {
        ValidateGuid(errors, $"{path}.id", goal.Id);
        AddIf(
            errors,
            $"{path}.name",
            string.IsNullOrWhiteSpace(goal.Name) || goal.Name.Length > MaxGoalNameLength,
            $"Name must be 1-{MaxGoalNameLength} characters.");
        AddIf(
            errors,
            $"{path}.iconEmoji",
            string.IsNullOrWhiteSpace(goal.IconEmoji) || goal.IconEmoji.Length > MaxEmojiLength,
            $"IconEmoji must be 1-{MaxEmojiLength} characters.");
        AddIf(
            errors,
            $"{path}.points",
            goal.Points < (goal.IsWeeklyOnly ? 0 : 1) || goal.Points > 99,
            "Points must be 1-99 for daily goals and 0-99 for weekly goals.");
        ValidateTimestamp(errors, $"{path}.updatedAt", goal.UpdatedAt);
        AddIf(
            errors,
            $"{path}.deletedAt",
            goal.IsDeleted && !goal.DeletedAt.HasValue,
            "DeletedAt is required for deleted goals.");
    }

    public void ValidateRecord(
        Dictionary<string, List<string>> errors,
        string path,
        DailyRecordContract record)
    {
        ValidateGuid(errors, $"{path}.id", record.Id);
        AddIf(errors, $"{path}.date", !TryParseDate(record.Date, out _), "Date must use yyyy-MM-dd.");
        ValidateTimestamp(errors, $"{path}.updatedAt", record.UpdatedAt);
        AddIf(
            errors,
            $"{path}.entries",
            record.Entries.Count > MaxEntriesPerRecord,
            $"A record may contain at most {MaxEntriesPerRecord} entries.");

        for (var index = 0; index < record.Entries.Count; index++)
        {
            var entry = record.Entries[index];
            var entryPath = $"{path}.entries[{index}]";
            ValidateGuid(errors, $"{entryPath}.id", entry.Id);
            ValidateGuid(errors, $"{entryPath}.goalId", entry.GoalId);
            AddIf(
                errors,
                $"{entryPath}.goalName",
                string.IsNullOrWhiteSpace(entry.GoalName) ||
                entry.GoalName.Length > MaxGoalNameLength,
                $"GoalName must be 1-{MaxGoalNameLength} characters.");
            AddIf(
                errors,
                $"{entryPath}.iconEmoji",
                string.IsNullOrWhiteSpace(entry.IconEmoji) ||
                entry.IconEmoji.Length > MaxEmojiLength,
                $"IconEmoji must be 1-{MaxEmojiLength} characters.");
            AddIf(
                errors,
                $"{entryPath}.goalPoints",
                entry.GoalPoints < (entry.IsWeeklyOnly ? 0 : 1) ||
                entry.GoalPoints > 99,
                "GoalPoints are outside the allowed range.");
            ValidateTimestamp(errors, $"{entryPath}.updatedAt", entry.UpdatedAt);
        }
    }

    public void ValidateMeasurement(
        Dictionary<string, List<string>> errors,
        string path,
        MeasurementContract measurement)
    {
        ValidateGuid(errors, $"{path}.id", measurement.Id);
        AddIf(
            errors,
            $"{path}.date",
            !TryParseDate(measurement.Date, out _),
            "Date must use yyyy-MM-dd.");
        AddIf(
            errors,
            path,
            !measurement.WeightLbs.HasValue &&
            !measurement.BodyFatPercent.HasValue &&
            string.IsNullOrWhiteSpace(measurement.Notes),
            "A measurement requires weight, body fat, or notes.");
        AddIf(
            errors,
            $"{path}.weightLbs",
            measurement.WeightLbs.HasValue &&
            (!double.IsFinite(measurement.WeightLbs.Value) ||
             measurement.WeightLbs.Value <= 0 ||
             measurement.WeightLbs.Value > 1500),
            "WeightLbs must be greater than 0 and no more than 1500.");
        AddIf(
            errors,
            $"{path}.bodyFatPercent",
            measurement.BodyFatPercent.HasValue &&
            (!double.IsFinite(measurement.BodyFatPercent.Value) ||
             measurement.BodyFatPercent.Value < 0 ||
             measurement.BodyFatPercent.Value > 100),
            "BodyFatPercent must be between 0 and 100.");
        AddIf(
            errors,
            $"{path}.notes",
            measurement.Notes?.Length > MaxNotesLength,
            $"Notes must not exceed {MaxNotesLength} characters.");
        ValidateTimestamp(errors, $"{path}.updatedAt", measurement.UpdatedAt);
    }

    public static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    public static void ValidateCount(
        Dictionary<string, List<string>> errors,
        string path,
        int count) =>
        AddIf(
            errors,
            path,
            count > MaxBatchSize,
            $"Collection may contain at most {MaxBatchSize} items.");

    public static void ValidateGuid(
        Dictionary<string, List<string>> errors,
        string path,
        string value) =>
        AddIf(errors, path, !Guid.TryParse(value, out _), "Value must be a GUID.");

    public static void ValidateTimestamp(
        Dictionary<string, List<string>> errors,
        string path,
        DateTimeOffset value) =>
        AddIf(
            errors,
            path,
            value == default || value.Offset != TimeSpan.Zero,
            "Timestamp must be a non-default UTC value.");

    public static void AddIf(
        Dictionary<string, List<string>> errors,
        string path,
        bool condition,
        string message)
    {
        if (!condition)
            return;
        if (!errors.TryGetValue(path, out var messages))
        {
            messages = [];
            errors[path] = messages;
        }
        messages.Add(message);
    }
}
