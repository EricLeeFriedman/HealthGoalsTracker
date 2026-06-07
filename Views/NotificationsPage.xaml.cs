using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsViewModel ViewModel;

    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    async void OnMasterToggled(object sender, ToggledEventArgs e)
    {
        await ViewModel.ToggleAllCommand.ExecuteAsync(e.Value);
    }
}
