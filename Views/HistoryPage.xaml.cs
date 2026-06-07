using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Views;

public partial class HistoryPage : ContentPage
{
    public HistoryViewModel ViewModel;

    public HistoryPage(HistoryViewModel vm)
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
}
