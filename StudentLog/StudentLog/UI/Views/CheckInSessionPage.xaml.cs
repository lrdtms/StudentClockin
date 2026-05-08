using StudentLog.UI.ViewModels;

namespace StudentLog.UI.Views;

public partial class CheckInSessionPage : ContentPage
{
    private readonly CheckInSessionViewModel _viewModel;

    public CheckInSessionPage(CheckInSessionViewModel viewModel)
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
