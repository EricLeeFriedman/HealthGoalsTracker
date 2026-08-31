using HealthGoalsTracker.Functions.Services;

namespace HealthGoalsTracker.Functions.Tests;

public class CloudRepositoryTests
{
    [Fact]
    public async Task SyncAsync_IsolatesAuthenticatedUserPartitions()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        request.Goals.Add(BackendTestData.Goal());

        await repository.SyncAsync("user-a", request, CancellationToken.None);

        Assert.Single(await repository.GetGoalsAsync("user-a", CancellationToken.None));
        Assert.Empty(await repository.GetGoalsAsync("user-b", CancellationToken.None));
    }

    [Fact]
    public async Task SyncAsync_ReplayDoesNotCreateAnotherChange()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        request.Goals.Add(BackendTestData.Goal());

        var first = await repository.SyncAsync("user", request, CancellationToken.None);
        var sequenceAfterFirst = repository.Partitions["user"].Sequence;
        var second = await repository.SyncAsync("user", request, CancellationToken.None);

        Assert.Equal(sequenceAfterFirst, repository.Partitions["user"].Sequence);
        Assert.Equal(first.Cursor, second.Cursor);
        Assert.Equal(first.Goals.Select(goal => goal.Id), second.Goals.Select(goal => goal.Id));
    }

    [Fact]
    public async Task SyncAsync_CursorReturnsOnlyLaterChanges()
    {
        var repository = BackendTestData.Repository();
        var firstRequest = BackendTestData.SyncRequest();
        firstRequest.Goals.Add(BackendTestData.Goal());
        var first = await repository.SyncAsync("user", firstRequest, CancellationToken.None);
        var secondRequest = BackendTestData.SyncRequest();
        secondRequest.Cursor = first.Cursor;
        secondRequest.Measurements.Add(BackendTestData.Measurement());

        var second = await repository.SyncAsync("user", secondRequest, CancellationToken.None);

        Assert.Empty(second.Goals);
        Assert.Single(second.Measurements);
        Assert.NotEqual(first.Cursor, second.Cursor);
    }

    [Fact]
    public async Task SyncAsync_OlderUpdateDoesNotReplaceWinnerAndReturnsAuthority()
    {
        var repository = BackendTestData.Repository();
        var id = Guid.NewGuid().ToString();
        var newest = BackendTestData.Goal(id, BackendTestData.Timestamp(2));
        newest.Name = "Newest";
        var firstRequest = BackendTestData.SyncRequest();
        firstRequest.Goals.Add(newest);
        await repository.SyncAsync("user", firstRequest, CancellationToken.None);

        var candidate = BackendTestData.Goal(id, BackendTestData.Timestamp(1));
        candidate.Name = "Candidate";
        var request = BackendTestData.SyncRequest();
        request.Goals.Add(candidate);
        var response = await repository.SyncAsync("user", request, CancellationToken.None);

        var stored = Assert.Single(
            await repository.GetGoalsAsync("user", CancellationToken.None));
        Assert.Equal("Newest", stored.Name);
        Assert.Equal("Newest", Assert.Single(response.Goals).Name);
    }

    [Fact]
    public async Task SyncAsync_EqualTimestampWinnerIsIndependentOfArrivalOrder()
    {
        var id = Guid.NewGuid().ToString();
        var first = BackendTestData.Goal(id);
        first.Name = "Alpha";
        var second = BackendTestData.Goal(id);
        second.Name = "Zulu";

        var forwardWinner = await StoreInOrder(first, second);
        var reverseWinner = await StoreInOrder(second, first);

        Assert.Equal(forwardWinner.Name, reverseWinner.Name);
    }

    [Fact]
    public async Task SyncAsync_EqualTimestampLoserReceivesAuthoritativeWinner()
    {
        var repository = BackendTestData.Repository();
        var id = Guid.NewGuid().ToString();
        var candidates = new[] { BackendTestData.Goal(id), BackendTestData.Goal(id) };
        candidates[0].Name = "Alpha";
        candidates[1].Name = "Zulu";
        var canonical = candidates
            .OrderBy(goal => System.Text.Json.JsonSerializer.Serialize(goal), StringComparer.Ordinal)
            .ToArray();
        var winningRequest = BackendTestData.SyncRequest();
        winningRequest.Goals.Add(canonical[1]);
        await repository.SyncAsync("user", winningRequest, CancellationToken.None);
        var losingRequest = BackendTestData.SyncRequest();
        losingRequest.Goals.Add(canonical[0]);

        var response = await repository.SyncAsync("user", losingRequest, CancellationToken.None);

        Assert.Equal(canonical[1].Name, Assert.Single(response.Goals).Name);
    }

    [Fact]
    public async Task SyncAsync_RejectsCursorAheadOfUserPartition()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        request.Cursor = BackendTestData.CursorCodec().Encode("user", 1);

        await Assert.ThrowsAsync<InvalidCursorException>(
            () => repository.SyncAsync("user", request, CancellationToken.None));
    }

    public static async Task<HealthGoalsTracker.Functions.Contracts.GoalContract> StoreInOrder(
        params HealthGoalsTracker.Functions.Contracts.GoalContract[] goals)
    {
        var repository = BackendTestData.Repository();
        foreach (var goal in goals)
        {
            var request = BackendTestData.SyncRequest();
            request.Goals.Add(goal);
            await repository.SyncAsync("user", request, CancellationToken.None);
        }
        return Assert.Single(await repository.GetGoalsAsync("user", CancellationToken.None));
    }

    [Fact]
    public async Task SyncAsync_RecalculatesDailyTotalsFromSnapshots()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        request.DailyRecords.Add(BackendTestData.Record());

        var response = await repository.SyncAsync("user", request, CancellationToken.None);

        var record = Assert.Single(response.DailyRecords);
        Assert.Equal(3, record.TotalPointsEarned);
        Assert.Equal(3, record.TotalPointsPossible);
    }

    [Fact]
    public async Task SyncAsync_PreservesGoalTombstone()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        var goal = BackendTestData.Goal();
        goal.IsDeleted = true;
        goal.DeletedAt = BackendTestData.Timestamp();
        request.Goals.Add(goal);

        await repository.SyncAsync("user", request, CancellationToken.None);

        var stored = Assert.Single(
            await repository.GetGoalsAsync("user", CancellationToken.None));
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task GetMeasurementsAsync_FiltersInclusiveDateRange()
    {
        var repository = BackendTestData.Repository();
        var request = BackendTestData.SyncRequest();
        request.Measurements =
        [
            BackendTestData.Measurement(date: "2026-08-01"),
            BackendTestData.Measurement(date: "2026-08-15"),
            BackendTestData.Measurement(date: "2026-08-31")
        ];
        await repository.SyncAsync("user", request, CancellationToken.None);

        var results = await repository.GetMeasurementsAsync(
            "user",
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 20),
            CancellationToken.None);

        Assert.Equal("2026-08-15", Assert.Single(results).Date);
    }
}
