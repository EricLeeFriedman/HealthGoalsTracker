using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker.Views;

public partial class MeasurementsPage : ContentPage
{
    public MeasurementsViewModel ViewModel;

    public MeasurementsPage(MeasurementsViewModel vm)
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
