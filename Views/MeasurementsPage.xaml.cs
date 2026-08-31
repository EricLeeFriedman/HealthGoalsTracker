using HealthGoalsTracker.ViewModels;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Views;

public partial class MeasurementsPage : ContentPage
{
    public MeasurementsViewModel ViewModel;
    public ILogger<MeasurementsPage> Logger;

    public MeasurementsPage(
        MeasurementsViewModel viewModel,
        ILogger<MeasurementsPage> logger)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Logger = logger;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
        Logger.LogInformation("Measurements page loaded");
    }
}
