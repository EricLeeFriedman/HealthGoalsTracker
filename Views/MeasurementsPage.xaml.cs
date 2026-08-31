using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Views;

public partial class MeasurementsPage : ContentPage
{
    public MeasurementsViewModel ViewModel;

    public MeasurementsPage(MeasurementsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
