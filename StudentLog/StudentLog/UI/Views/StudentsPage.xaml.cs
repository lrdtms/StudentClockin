using StudentLog.UI.ViewModels;

namespace StudentLog.UI.Views;

public partial class StudentsPage : ContentPage
{
    private readonly StudentsViewModel _viewModel;

    public StudentsPage(StudentsViewModel viewModel)
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
