using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.ViewModels;

public partial class MeasurementsViewModel : ObservableObject
{
    public IMeasurementService MeasurementService;

    [ObservableProperty]
    DateTime _entryDate = DateTime.Today;

    [ObservableProperty]
    string _weightText = "";

    [ObservableProperty]
    string _bodyFatText = "";

    [ObservableProperty]
    string _notes = "";

    [ObservableProperty]
    ObservableCollection<BodyMeasurement> _recentMeasurements = [];

    [ObservableProperty]
    ObservableCollection<BodyMeasurement> _chartMeasurements = [];

    [ObservableProperty]
    bool _isLoading;

    public bool HasMeasurements => RecentMeasurements.Count > 0;
    public bool NoMeasurements => !HasMeasurements;

    public MeasurementsViewModel(IMeasurementService measurementService)
    {
        MeasurementService = measurementService;
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            await RefreshMeasurementsAsync();
            await LoadMeasurementForSelectedDateAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnEntryDateChanged(DateTime value)
    {
        _ = LoadMeasurementForSelectedDateAsync();
    }

    [RelayCommand]
    async Task SaveMeasurementAsync()
    {
        var weight = TryParseNullableDouble(WeightText, "weight");
        if (weight.HasError)
        {
            await ShowAlertAsync("Invalid Number", $"Please enter a valid {weight.FieldName}.");
            return;
        }

        var bodyFat = TryParseNullableDouble(BodyFatText, "body fat");
        if (bodyFat.HasError)
        {
            await ShowAlertAsync("Invalid Number", $"Please enter a valid {bodyFat.FieldName}.");
            return;
        }

        if (weight.Value.HasValue && weight.Value.Value <= 0)
        {
            await ShowAlertAsync("Invalid Weight", "Weight must be greater than 0.");
            return;
        }

        if (bodyFat.Value.HasValue && (bodyFat.Value.Value < 0 || bodyFat.Value.Value > 100))
        {
            await ShowAlertAsync("Invalid Body Fat", "Body fat must be between 0 and 100.");
            return;
        }

        var trimmedNotes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        if (!weight.Value.HasValue && !bodyFat.Value.HasValue && string.IsNullOrWhiteSpace(trimmedNotes))
        {
            await ShowAlertAsync("Nothing To Save", "Enter a weight, body fat %, or note before saving.");
            return;
        }

        var selectedDate = DateOnly.FromDateTime(EntryDate);
        var existing = await MeasurementService.GetMeasurementForDateAsync(selectedDate);

        var measurement = existing ?? new BodyMeasurement();
        measurement.Date = selectedDate.ToString("yyyy-MM-dd");
        measurement.WeightLbs = weight.Value;
        measurement.BodyFatPercent = bodyFat.Value;
        measurement.Notes = trimmedNotes;

        await MeasurementService.SaveMeasurementAsync(measurement);
        await RefreshMeasurementsAsync();
        await LoadMeasurementForSelectedDateAsync();
    }

    [RelayCommand]
    async Task SelectMeasurementAsync(BodyMeasurement? measurement)
    {
        if (measurement == null) return;

        var selectedDate = measurement.MeasurementDate.ToDateTime(TimeOnly.MinValue);
        if (EntryDate.Date == selectedDate.Date)
        {
            await LoadMeasurementForSelectedDateAsync();
            return;
        }

        EntryDate = selectedDate;
    }

    async Task RefreshMeasurementsAsync()
    {
        var measurements = await MeasurementService.GetMeasurementsAsync();

        RecentMeasurements.Clear();
        foreach (var measurement in measurements.OrderByDescending(m => m.Date))
            RecentMeasurements.Add(measurement);

        ChartMeasurements.Clear();
        foreach (var measurement in measurements
                     .Where(m => m.WeightLbs.HasValue || m.BodyFatPercent.HasValue)
                     .OrderBy(m => m.Date))
            ChartMeasurements.Add(measurement);

        OnPropertyChanged(nameof(HasMeasurements));
        OnPropertyChanged(nameof(NoMeasurements));
    }

    async Task LoadMeasurementForSelectedDateAsync()
    {
        var measurement = await MeasurementService.GetMeasurementForDateAsync(DateOnly.FromDateTime(EntryDate));
        WeightText = measurement?.WeightLbs?.ToString("0.##") ?? "";
        BodyFatText = measurement?.BodyFatPercent?.ToString("0.##") ?? "";
        Notes = measurement?.Notes ?? "";
    }

    async Task ShowAlertAsync(string title, string message)
    {
        await GetCurrentPage().DisplayAlertAsync(title, message, "OK");
    }

    (double? Value, bool HasError, string FieldName) TryParseNullableDouble(string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, false, fieldName);

        if (double.TryParse(text.Trim(), out var value))
            return (value, false, fieldName);

        return (null, true, fieldName);
    }

    static Page GetCurrentPage() =>
        Application.Current!.Windows[0].Page
            ?? throw new InvalidOperationException("No active page found.");
}
