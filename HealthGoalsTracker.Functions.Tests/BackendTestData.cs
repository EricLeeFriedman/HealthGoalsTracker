using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HealthGoalsTracker.Functions.Contracts;
using HealthGoalsTracker.Functions.Services;
using Microsoft.AspNetCore.Http;

namespace HealthGoalsTracker.Functions.Tests;

public static class BackendTestData
{
    public const string CursorSigningKey = "backend-test-cursor-signing-key-1234567890";
    public const string ApiScope = "health-goals.sync";

    public static CursorCodec CursorCodec() => new(CursorSigningKey);
    public static InMemoryCloudRepository Repository() => new(CursorCodec());
    public static ContractValidator Validator() => new(CursorCodec());

    public static DateTimeOffset Timestamp(int minute = 0) =>
        new(2026, 8, 31, 12, minute, 0, TimeSpan.Zero);

    public static GoalContract Goal(
        string? id = null,
        DateTimeOffset? updatedAt = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = "Sleep",
        IconEmoji = "😴",
        Points = 3,
        SortOrder = 0,
        UpdatedAt = updatedAt ?? Timestamp()
    };

    public static DailyRecordContract Record(
        string? id = null,
        DateTimeOffset? updatedAt = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Date = "2026-08-31",
        TotalPointsEarned = 99,
        TotalPointsPossible = 99,
        UpdatedAt = updatedAt ?? Timestamp(),
        Entries =
        [
            new DailyGoalEntryContract
            {
                Id = Guid.NewGuid().ToString(),
                GoalId = Guid.NewGuid().ToString(),
                GoalName = "Sleep",
                IconEmoji = "😴",
                GoalPoints = 3,
                IsCompleted = true,
                UpdatedAt = updatedAt ?? Timestamp()
            },
            new DailyGoalEntryContract
            {
                Id = Guid.NewGuid().ToString(),
                GoalId = Guid.NewGuid().ToString(),
                GoalName = "Strength",
                IconEmoji = "💪",
                GoalPoints = 0,
                IsWeeklyOnly = true,
                IsCompleted = true,
                UpdatedAt = updatedAt ?? Timestamp()
            }
        ]
    };

    public static MeasurementContract Measurement(
        string? id = null,
        string date = "2026-08-31",
        DateTimeOffset? updatedAt = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Date = date,
        WeightLbs = 180,
        BodyFatPercent = 20,
        UpdatedAt = updatedAt ?? Timestamp()
    };

    public static SyncRequest SyncRequest() => new()
    {
        DeviceId = Guid.NewGuid().ToString()
    };

    public static DefaultHttpContext HttpContext(
        object? body = null,
        string? subject = null,
        string? correlationId = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (body != null)
        {
            var json = body is string text
                ? text
                : JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            context.Request.ContentType = "application/json";
        }
        if (subject != null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("sub", subject), new Claim("scp", ApiScope)],
                    "test"));
        }
        if (correlationId != null)
            context.Request.Headers["X-Correlation-Id"] = correlationId;
        return context;
    }
}
