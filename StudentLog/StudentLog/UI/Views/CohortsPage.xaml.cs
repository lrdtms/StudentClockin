using StudentLog.UI.ViewModels;

namespace StudentLog.UI.Views;

public partial class CohortsPage : ContentPage
{
    private readonly CohortsViewModel _viewModel;

    public CohortsPage(CohortsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
