using HealthGoalsTracker.Functions.Services;

namespace HealthGoalsTracker.Functions.Tests;

public class ContractValidatorTests
{
    [Fact]
    public void ValidateSync_AcceptsPullOnlyRequest()
    {
        var request = BackendTestData.SyncRequest();

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSync_RejectsInvalidCursorAndDevice()
    {
        var request = BackendTestData.SyncRequest();
        request.DeviceId = "device";
        request.Cursor = "not-a-cursor";

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Contains("deviceId", errors);
        Assert.Contains("cursor", errors);
    }

    [Fact]
    public void ValidateSync_RejectsForgedAndCrossUserCursors()
    {
        var codec = BackendTestData.CursorCodec();
        var request = BackendTestData.SyncRequest();
        request.Cursor = codec.Encode("user-a", 1);
        request.Cursor = request.Cursor[..^1] + (request.Cursor[^1] == 'A' ? "B" : "A");

        var forged = BackendTestData.Validator().ValidateSync("user-a", request);
        request.Cursor = codec.Encode("user-a", 1);
        var crossUser = BackendTestData.Validator().ValidateSync("user-b", request);

        Assert.Contains("cursor", forged);
        Assert.Contains("cursor", crossUser);
    }

    [Fact]
    public void ValidateSync_RejectsMoreThanOneHundredEntities()
    {
        var request = BackendTestData.SyncRequest();
        request.Goals = Enumerable.Range(0, 101)
            .Select(_ => BackendTestData.Goal())
            .ToList();

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Contains("goals", errors);
    }

    [Theory]
    [InlineData("2026-8-31")]
    [InlineData("08/31/2026")]
    [InlineData("2026-02-30")]
    public void ValidateSync_RejectsNonCanonicalDates(string date)
    {
        var request = BackendTestData.SyncRequest();
        request.Measurements.Add(BackendTestData.Measurement(date: date));

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Contains("measurements[0].date", errors);
    }

    [Fact]
    public void ValidateSync_RejectsMeasurementWithoutContent()
    {
        var measurement = BackendTestData.Measurement();
        measurement.WeightLbs = null;
        measurement.BodyFatPercent = null;
        measurement.Notes = " ";
        var request = BackendTestData.SyncRequest();
        request.Measurements.Add(measurement);

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Contains("measurements[0]", errors);
    }

    [Fact]
    public void ValidateSync_RejectsInvalidDailyPointsButAllowsZeroWeeklyPoints()
    {
        var daily = BackendTestData.Goal();
        daily.Points = 0;
        var weekly = BackendTestData.Goal();
        weekly.IsWeeklyOnly = true;
        weekly.Points = 0;
        var request = BackendTestData.SyncRequest();
        request.Goals.AddRange([daily, weekly]);

        var errors = BackendTestData.Validator().ValidateSync("user", request);

        Assert.Contains("goals[0].points", errors);
        Assert.DoesNotContain("goals[1].points", errors);
    }

    [Fact]
    public void ValidateDateRange_RejectsReverseAndOversizedRanges()
    {
        var validator = BackendTestData.Validator();

        var reverse = validator.ValidateDateRange("2026-09-01", "2026-08-31");
        var oversized = validator.ValidateDateRange("2025-01-01", "2026-08-31");

        Assert.Contains("range", reverse);
        Assert.Contains("range", oversized);
    }
}
