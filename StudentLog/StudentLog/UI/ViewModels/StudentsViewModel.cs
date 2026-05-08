using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

public class StudentsViewModel : ObservableObject
{
    private readonly IStudentService _studentService;
    private readonly ICohortService _cohortService;
    private readonly INfcService _nfcService;

    public ObservableCollection<Cohort> Cohorts { get; } = new();
    public ObservableCollection<Student> AllStudentsForCohort { get; } = new();

    private Cohort? _selectedCohort;
    public Cohort? SelectedCohort
    {
        get => _selectedCohort;
        set
        {
            if (SetProperty(ref _selectedCohort, value))
            {
                _ = LoadAllStudentsForCohortAsync();
            }
        }
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _surname = string.Empty;
    public string Surname
    {
        get => _surname;
        set => SetProperty(ref _surname, value);
    }

    private string _uid = string.Empty;
    public string Uid
    {
        get => _uid;
        set => SetProperty(ref _uid, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Student Management Properties
    private Student? _selectedStudentForEdit;
    public Student? SelectedStudentForEdit
    {
        get => _selectedStudentForEdit;
        set => SetProperty(ref _selectedStudentForEdit, value);
    }

    private string _editStudentName = string.Empty;
    public string EditStudentName
    {
        get => _editStudentName;
        set => SetProperty(ref _editStudentName, value);
    }

    private string _editStudentSurname = string.Empty;
    public string EditStudentSurname
    {
        get => _editStudentSurname;
        set => SetProperty(ref _editStudentSurname, value);
    }

    private string _editStudentUid = string.Empty;
    public string EditStudentUid
    {
        get => _editStudentUid;
        set => SetProperty(ref _editStudentUid, value);
    }

    private bool _isEditingStudent;
    public bool IsEditingStudent
    {
        get => _isEditingStudent;
        set => SetProperty(ref _isEditingStudent, value);
    }

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand ScanUidCommand { get; }
    public IAsyncRelayCommand AddStudentCommand { get; }
    public IAsyncRelayCommand<Student> DeleteStudentCommand { get; }
    public IAsyncRelayCommand<Student> EditStudentCommand { get; }
    public IAsyncRelayCommand<Student> ViewHistoryCommand { get; }
    public IAsyncRelayCommand SaveEditedStudentCommand { get; }
    public IRelayCommand CancelEditCommand { get; }

    public StudentsViewModel(IStudentService studentService, ICohortService cohortService, INfcService nfcService)
    {
        _studentService = studentService;
        _cohortService = cohortService;
        _nfcService = nfcService;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ScanUidCommand = new AsyncRelayCommand(ScanUidAsync);
        AddStudentCommand = new AsyncRelayCommand(AddStudentAsync);
        DeleteStudentCommand = new AsyncRelayCommand<Student>(DeleteStudentAsync);
        EditStudentCommand = new AsyncRelayCommand<Student>(EditStudentAsync);
        ViewHistoryCommand = new AsyncRelayCommand<Student>(ViewHistoryAsync);
        SaveEditedStudentCommand = new AsyncRelayCommand(SaveEditedStudentAsync);
        CancelEditCommand = new RelayCommand(CancelEdit);
    }

    public async Task LoadAsync()
    {
        Cohorts.Clear();
        var cohorts = await _cohortService.GetCohortsAsync();
        foreach (var cohort in cohorts)
        {
            Cohorts.Add(cohort);
        }
    }

    public async Task ScanUidAsync()
    {
        var scanned = await _nfcService.ScanSingleUidAsync();
        if (!string.IsNullOrWhiteSpace(scanned))
        {
            Uid = scanned;
            StatusMessage = "UID scanned.";
        }
    }

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

    private void CancelEdit()
    {
        IsEditingStudent = false;
        SelectedStudentForEdit = null;
        EditStudentName = string.Empty;
        EditStudentSurname = string.Empty;
        EditStudentUid = string.Empty;
    }

    private async Task ViewHistoryAsync(Student? student)
    {
        if (student is null)
        {
            StatusMessage = "No student selected.";
            return;
        }

        try
        {
            // Navigate to student history page and pass student data
            await Shell.Current.GoToAsync($"studenthistory?studentId={student.Id}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error navigating to history: {ex.Message}";
        }
    }
}
