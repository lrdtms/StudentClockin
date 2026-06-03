using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentLog.Application.Csv;
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
    private readonly ISoundService _soundService;
    private readonly IDatabaseInitializationTracker _dbTracker;
    private readonly ICsvImportService _csvImportService;

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

    [ObservableProperty]
    private string? _editStudentClockInSoundPath;

    [ObservableProperty]
    private string? _editStudentClockOutSoundPath;

    partial void OnSelectedCohortChanged(Cohort? value) => _ = LoadAllStudentsForCohortAsync();

    public StudentsViewModel(
        IStudentService studentService,
        ICohortService cohortService,
        INfcService nfcService,
        IDialogService dialogService,
        ISoundService soundService,
        IDatabaseInitializationTracker dbTracker,
        ICsvImportService csvImportService)
    {
        _studentService = studentService;
        _cohortService = cohortService;
        _nfcService = nfcService;
        _dialogService = dialogService;
        _soundService = soundService;
        _dbTracker = dbTracker;
        _csvImportService = csvImportService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _dbTracker.WaitAsync();
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
    private async Task ImportCsvAsync()
    {
        if (SelectedCohort is null)
        {
            StatusMessage = "Please select a cohort first.";
            return;
        }

        CsvImportResult? result;
        try
        {
            result = await _csvImportService.PickAndParseAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            return;
        }

        if (result is null)
            return;

        var existing = await _studentService.GetStudentsAsync();
        var existingUids = new HashSet<string>(existing.Select(s => s.UID), StringComparer.OrdinalIgnoreCase);

        int imported = 0;
        int skipped = 0;
        int invalid = result.InvalidRowCount;

        foreach (var row in result.ValidRows)
        {
            if (existingUids.Contains(row.UID))
            {
                skipped++;
                continue;
            }

            try
            {
                await _studentService.AddStudentAsync(row.Name, row.Surname, row.UID, SelectedCohort.Id);
                existingUids.Add(row.UID);
                imported++;
            }
            catch (ArgumentException)
            {
                invalid++;
            }
        }

        await LoadAllStudentsForCohortAsync();

        var parts = new List<string>();
        if (imported > 0) parts.Add($"Imported {imported} student{(imported == 1 ? "" : "s")}");
        if (skipped > 0) parts.Add($"{skipped} duplicate{(skipped == 1 ? "" : "s")} skipped");
        if (invalid > 0) parts.Add($"{invalid} invalid row{(invalid == 1 ? "" : "s")}");
        StatusMessage = parts.Count > 0 ? string.Join(". ", parts) + "." : "Nothing to import.";
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
        EditStudentClockInSoundPath = student.ClockInSoundPath;
        EditStudentClockOutSoundPath = student.ClockOutSoundPath;
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
            SelectedStudentForEdit.ClockInSoundPath = EditStudentClockInSoundPath;
            SelectedStudentForEdit.ClockOutSoundPath = EditStudentClockOutSoundPath;

            await _studentService.UpdateStudentAsync(SelectedStudentForEdit);
            StatusMessage = $"Student {EditStudentName} {EditStudentSurname} updated.";
            IsEditingStudent = false;
            EditStudentClockInSoundPath = null;
            EditStudentClockOutSoundPath = null;
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
        EditStudentClockInSoundPath = null;
        EditStudentClockOutSoundPath = null;
    }

    [RelayCommand]
    private async Task SetStudentClockInSoundAsync()
    {
        var path = await _soundService.UploadSoundAsync();
        if (path is null) return;

        EditStudentClockInSoundPath = path;
    }

    [RelayCommand]
    private async Task SetStudentClockOutSoundAsync()
    {
        var path = await _soundService.UploadSoundAsync();
        if (path is null) return;

        EditStudentClockOutSoundPath = path;
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
