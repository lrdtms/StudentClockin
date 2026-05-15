using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

public partial class StudentsViewModel : ObservableObject
{
    private readonly IStudentService _studentService;
    private readonly ICohortService _cohortService;
    private readonly INfcService _nfcService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<Cohort> Cohorts { get; } = new();
    public ObservableCollection<Student> AllStudentsForCohort { get; } = new();

    [ObservableProperty]
    private Cohort? _selectedCohort;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _surname = string.Empty;

    [ObservableProperty]
    private string _uid = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private Student? _selectedStudentForEdit;

    [ObservableProperty]
    private string _editStudentName = string.Empty;

    [ObservableProperty]
    private string _editStudentSurname = string.Empty;

    [ObservableProperty]
    private string _editStudentUid = string.Empty;

    [ObservableProperty]
    private bool _isEditingStudent;

    partial void OnSelectedCohortChanged(Cohort? value) => _ = LoadAllStudentsForCohortAsync();

    public StudentsViewModel(
        IStudentService studentService,
        ICohortService cohortService,
        INfcService nfcService,
        IDialogService dialogService)
    {
        _studentService = studentService;
        _cohortService = cohortService;
        _nfcService = nfcService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Cohorts.Clear();
        var cohorts = await _cohortService.GetCohortsAsync();
        foreach (var cohort in cohorts)
        {
            Cohorts.Add(cohort);
        }
    }

    [RelayCommand]
    public async Task ScanUidAsync()
    {
        var scanned = await _nfcService.ScanSingleUidAsync();

        if (!string.IsNullOrWhiteSpace(scanned))
        {
            Uid = scanned;
            StatusMessage = "UID scanned.";
            return;
        }

        var manual = await _dialogService.PromptAsync(
            "Scan NFC",
            "No UID was detected from ACR122U. Scan again or enter UID manually.",
            accept: "Save",
            cancel: "Cancel",
            placeholder: "UID");

        if (!string.IsNullOrWhiteSpace(manual))
        {
            Uid = manual.Trim();
            StatusMessage = "UID entered manually.";
        }
    }

    [RelayCommand]
    public async Task AddStudentAsync()
    {
        try
        {
            await _studentService.AddStudentAsync(Name, Surname, Uid, SelectedCohort?.Id ?? 0);
            Name = string.Empty;
            Surname = string.Empty;
            Uid = string.Empty;
            StatusMessage = "Student added.";
            await LoadAllStudentsForCohortAsync();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteStudentAsync(Student student)
    {
        if (student is null)
        {
            StatusMessage = "No student selected.";
            return;
        }

        try
        {
            await _studentService.DeleteStudentAsync(student.Id);
            StatusMessage = $"Student {student.Name} {student.Surname} deleted.";
            await LoadAllStudentsForCohortAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting student: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task EditStudentAsync(Student student)
    {
        if (student is null)
        {
            StatusMessage = "No student selected.";
            return Task.CompletedTask;
        }

        SelectedStudentForEdit = student;
        EditStudentName = student.Name;
        EditStudentSurname = student.Surname;
        EditStudentUid = student.UID;
        IsEditingStudent = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ViewHistoryAsync(Student? student)
    {
        if (student is null)
        {
            StatusMessage = "No student selected.";
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"studenthistory?studentId={student.Id}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error navigating to history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveEditedStudentAsync()
    {
        if (SelectedStudentForEdit is null)
        {
            StatusMessage = "No student selected.";
            return;
        }

        try
        {
            SelectedStudentForEdit.Name = EditStudentName;
            SelectedStudentForEdit.Surname = EditStudentSurname;
            SelectedStudentForEdit.UID = EditStudentUid;

            await _studentService.UpdateStudentAsync(SelectedStudentForEdit);
            StatusMessage = $"Student {EditStudentName} {EditStudentSurname} updated.";
            IsEditingStudent = false;
            SelectedStudentForEdit = null;
            await LoadAllStudentsForCohortAsync();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = $"Validation error: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating student: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditingStudent = false;
        SelectedStudentForEdit = null;
        EditStudentName = string.Empty;
        EditStudentSurname = string.Empty;
        EditStudentUid = string.Empty;
    }

    private async Task LoadAllStudentsForCohortAsync()
    {
        if (SelectedCohort is null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AllStudentsForCohort.Clear();
            });
            return;
        }

        var students = await _studentService.GetStudentsAsync(SelectedCohort.Id);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllStudentsForCohort.Clear();
            foreach (var student in students)
            {
                AllStudentsForCohort.Add(student);
            }
        });
    }
}
