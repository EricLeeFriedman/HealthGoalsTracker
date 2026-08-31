using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthGoalsTracker.Models;
using HealthGoalsTracker.Services;

namespace HealthGoalsTracker.ViewModels;

public partial class MeasurementsViewModel : ObservableObject
{
    public IMeasurementService MeasurementService { get; }

    [ObservableProperty]
    public partial ObservableCollection<BodyMeasurement> Measurements { get; set; } = new();

    [ObservableProperty]
    public partial double? WeightLbs { get; set; }

    [ObservableProperty]
    public partial double? BodyFatPercent { get; set; }

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime SelectedDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public MeasurementsViewModel(IMeasurementService measurementService)
    {
        MeasurementService = measurementService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            var list = await MeasurementService.GetMeasurementsAsync();
            Measurements.Clear();
            foreach (var m in list)
            {
                Measurements.Add(m);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    public async Task SaveMeasurementAsync()
    {
        var newEntry = new BodyMeasurement
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "local", // Placeholder until Auth is implemented
            Date = SelectedDate.ToString("yyyy-MM-dd"),
            WeightLbs = WeightLbs,
            BodyFatPercent = BodyFatPercent,
            Notes = Notes,
            UpdatedAt = DateTime.UtcNow
        };

        await MeasurementService.SaveMeasurementAsync(newEntry);
        WeightLbs = null;
        BodyFatPercent = null;
        Notes = string.Empty;
        await LoadAsync();
    }
}