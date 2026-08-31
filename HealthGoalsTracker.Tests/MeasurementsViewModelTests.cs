using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;
using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Tests;

public class MeasurementsViewModelTests
{
    [Theory]
    [InlineData("", null, false)]
    [InlineData("  ", null, false)]
    [InlineData("180.5", 180.5, false)]
    [InlineData("not-a-number", null, true)]
    public void TryParseNullableDouble_HandlesOptionalNumericInput(
        string text,
        double? expected,
        bool expectedError)
    {
        var viewModel = new MeasurementsViewModel(new RecordingMeasurementService());

        var result = viewModel.TryParseNullableDouble(text, "weight");

        Assert.Equal(expected, result.Value);
        Assert.Equal(expectedError, result.HasError);
        Assert.Equal("weight", result.FieldName);
    }

    [Fact]
    public async Task LoadAsync_OrdersRecentEntriesNewestFirstAndChartEntriesOldestFirst()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = new RecordingMeasurementService
        {
            Measurements =
            [
                new() { Date = today.AddDays(-1).ToString("yyyy-MM-dd"), Notes = "Notes only" },
                new() { Date = today.ToString("yyyy-MM-dd"), WeightLbs = 180 },
                new() { Date = today.AddDays(-2).ToString("yyyy-MM-dd"), BodyFatPercent = 20 }
            ]
        };
        var viewModel = new MeasurementsViewModel(service);

        await viewModel.LoadAsync();

        Assert.Equal(
            [today, today.AddDays(-1), today.AddDays(-2)],
            viewModel.RecentMeasurements.Select(item => item.MeasurementDate));
        Assert.Equal(
            [today.AddDays(-2), today],
            viewModel.ChartMeasurements.Select(item => item.MeasurementDate));
        Assert.True(viewModel.HasMeasurements);
        Assert.False(viewModel.NoMeasurements);
        Assert.Equal("180", viewModel.WeightText);
    }

    [Fact]
    public async Task SelectMeasurementCommand_LoadsSelectedEntryIntoForm()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var measurement = new BodyMeasurement
        {
            Date = today.ToString("yyyy-MM-dd"),
            WeightLbs = 175.5,
            BodyFatPercent = 19.25,
            Notes = "Morning"
        };
        var service = new RecordingMeasurementService { Measurements = [measurement] };
        var viewModel = new MeasurementsViewModel(service);

        await viewModel.SelectMeasurementCommand.ExecuteAsync(measurement);

        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), viewModel.EntryDate);
        Assert.Equal("175.5", viewModel.WeightText);
        Assert.Equal("19.25", viewModel.BodyFatText);
        Assert.Equal("Morning", viewModel.Notes);
    }

    [Theory]
    [InlineData("0", "", "", "Invalid Weight")]
    [InlineData("-1", "", "", "Invalid Weight")]
    [InlineData("", "-0.1", "", "Invalid Body Fat")]
    [InlineData("", "100.1", "", "Invalid Body Fat")]
    [InlineData("", "", "", "Nothing To Save")]
    [InlineData("invalid", "", "", "Invalid Number")]
    [InlineData("", "invalid", "", "Invalid Number")]
    public async Task SaveMeasurementCommand_RejectsInvalidOrEmptyInput(
        string weight,
        string bodyFat,
        string notes,
        string expectedAlertTitle)
    {
        var service = new RecordingMeasurementService();
        var alerts = new List<(string Title, string Message)>();
        var viewModel = new MeasurementsViewModel(service)
        {
            WeightText = weight,
            BodyFatText = bodyFat,
            Notes = notes,
            AlertHandler = (title, message) =>
            {
                alerts.Add((title, message));
                return Task.CompletedTask;
            }
        };

        await viewModel.SaveMeasurementCommand.ExecuteAsync(null);

        Assert.Empty(service.Measurements);
        Assert.Equal(expectedAlertTitle, Assert.Single(alerts).Title);
    }

    [Fact]
    public async Task SaveMeasurementCommand_TrimsAndSavesNotesOnlyEntry()
    {
        var service = new RecordingMeasurementService();
        var viewModel = new MeasurementsViewModel(service)
        {
            EntryDate = new DateTime(2026, 8, 31),
            Notes = "  Recovery day  "
        };

        await viewModel.SaveMeasurementCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.Measurements);
        Assert.Equal("2026-08-31", saved.Date);
        Assert.Null(saved.WeightLbs);
        Assert.Null(saved.BodyFatPercent);
        Assert.Equal("Recovery day", saved.Notes);
    }
}

public class RecordingMeasurementService : IMeasurementService
{
    public List<BodyMeasurement> Measurements { get; set; } = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<List<BodyMeasurement>> GetMeasurementsAsync() =>
        Task.FromResult(Measurements.ToList());

    public Task<BodyMeasurement?> GetMeasurementForDateAsync(DateOnly date) =>
        Task.FromResult(
            Measurements.SingleOrDefault(item => item.Date == date.ToString("yyyy-MM-dd")));

    public Task SaveMeasurementAsync(BodyMeasurement measurement)
    {
        var existing = Measurements.SingleOrDefault(item => item.Date == measurement.Date);
        if (existing != null)
            Measurements.Remove(existing);
        Measurements.Add(measurement);
        return Task.CompletedTask;
    }
}
