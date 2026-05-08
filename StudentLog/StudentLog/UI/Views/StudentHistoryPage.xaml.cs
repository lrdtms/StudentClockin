using StudentLog.UI.ViewModels;

namespace StudentLog.UI.Views;

public partial class StudentHistoryPage : ContentPage
{
    private readonly StudentHistoryViewModel _viewModel;

    public StudentHistoryPage(StudentHistoryViewModel viewModel)
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
