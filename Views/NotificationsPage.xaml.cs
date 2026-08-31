using HealthGoalsTracker.ViewModels;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsViewModel ViewModel;
    public ILogger<NotificationsPage> Logger;

    public NotificationsPage(
        NotificationsViewModel vm,
        ILogger<NotificationsPage> logger)
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
        Logger.LogInformation("Notifications page loaded");
    }

    async void OnMasterToggled(object? sender, ToggledEventArgs e)
    {
        await ViewModel.ToggleAllCommand.ExecuteAsync(e.Value);
    }
}
