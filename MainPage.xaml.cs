using HealthGoalsTracker.ViewModels;

namespace HealthGoalsTracker
{
    public partial class MainPage : ContentPage
    {
        public MainViewModel ViewModel;

        public MainPage(MainViewModel viewModel)
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
}
