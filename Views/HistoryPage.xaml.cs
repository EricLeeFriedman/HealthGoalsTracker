using HealthGoalsTracker.ViewModels;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Views;

public partial class HistoryPage : ContentPage
{
    public HistoryViewModel ViewModel;
    public ILogger<HistoryPage> Logger;

    public HistoryPage(HistoryViewModel vm, ILogger<HistoryPage> logger)
    {
        InitializeComponent();
        ViewModel = vm;
        Logger = logger;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
        Logger.LogInformation("History page loaded");
    }
}
