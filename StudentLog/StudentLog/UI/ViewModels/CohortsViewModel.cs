using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using StudentLog.UI.Messaging;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

public partial class CohortsViewModel : ObservableObject
{
    private readonly ICohortService _cohortService;
    private readonly IStudentService _studentService;
    private readonly ICsvExportService _csvExportService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<CohortsViewModel> _logger;

    public ObservableCollection<Cohort> Cohorts { get; } = new();
    public ObservableCollection<Student> Students { get; } = new();

    public IReadOnlyList<string> SessionFilterScopes { get; } = new[] { "Day", "Month", "Year" };

    [ObservableProperty]
    private Cohort? _selectedCohort;

    [ObservableProperty]
    private string _selectedSessionFilterScope = "Day";

    [ObservableProperty]
    private DateTime _selectedSessionDate = DateTime.Today;

    [ObservableProperty]
    private string _newCohortName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isExporting;

    partial void OnSelectedCohortChanged(Cohort? value) => _ = LoadStudentsForSelectedAsync();
    partial void OnSelectedSessionFilterScopeChanged(string value) => _ = LoadStudentsForSelectedAsync();
    partial void OnSelectedSessionDateChanged(DateTime value) => _ = LoadStudentsForSelectedAsync();

    public CohortsViewModel(
        ICohortService cohortService,
        IStudentService studentService,
        ICsvExportService csvExportService,
        IDialogService dialogService,
        ILogger<CohortsViewModel> logger)
    {
        _cohortService = cohortService;
        _studentService = studentService;
        _csvExportService = csvExportService;
        _dialogService = dialogService;
        _logger = logger;

        WeakReferenceMessenger.Default.Register<AttendanceRecordedMessage>(this, async (recipient, message) =>
        {
            if (SelectedCohort?.Id == message.CohortId)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SelectedSessionDate = message.Timestamp.Date;
                });

                await LoadStudentsForSelectedAsync();
            }
        });
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

        await LoadStudentsForSelectedAsync();
    }

    [RelayCommand]
    public async Task AddCohortAsync()
    {
        try
        {
            await _cohortService.AddCohortAsync(NewCohortName);
            NewCohortName = string.Empty;
            await LoadAsync();
            StatusMessage = "Cohort added.";
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteCohortAsync(Cohort cohort)
    {
        var confirmed = await _dialogService.ConfirmAsync(
            "Delete Cohort",
            $"Delete '{cohort.Name}'? This will permanently delete all students and attendance records in this cohort.",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            await _cohortService.DeleteCohortAsync(cohort.Id);
            if (SelectedCohort?.Id == cohort.Id)
                SelectedCohort = null;
            await LoadAsync();
            StatusMessage = $"'{cohort.Name}' deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[COHORTS] Failed to delete cohort {Id}", cohort.Id);
            StatusMessage = "Failed to delete cohort.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync()
    {
        IsExporting = true;
        ExportCsvCommand.NotifyCanExecuteChanged();

        try
        {
            var records = Students.Select(s => new AttendanceRecord
            {
                StudentId = s.Id,
                StudentName = s.Name,
                StudentSurname = s.Surname,
                SignInTime = s.SignInTime,
                SignOutTime = s.SignOutTime
            }).ToList();

            var suggestedFileName = $"{SelectedCohort!.Name}_{SelectedSessionFilterScope}_{SelectedSessionDate:yyyy-MM-dd}"
                .Replace(' ', '_');

            await _csvExportService.ExportAttendanceAsync(records, suggestedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EXPORT] Export failed");
        }
        finally
        {
            IsExporting = false;
            ExportCsvCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExportCsv() => SelectedCohort is not null && Students.Count > 0 && !IsExporting;

    private async Task LoadStudentsForSelectedAsync()
    {
        if (SelectedCohort is null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Students.Clear();
                StatusMessage = "Select a cohort to view attendance sessions.";
            });
            ExportCsvCommand.NotifyCanExecuteChanged();
            return;
        }

        var selected = SelectedSessionDate;
        IReadOnlyList<Student> students;

        switch (SelectedSessionFilterScope)
        {
            case "Day":
            {
                var date = DateOnly.FromDateTime(selected);
                students = await _studentService.GetStudentsForDateAsync(SelectedCohort.Id, date);
                break;
            }
            case "Month":
            {
                var from = new DateOnly(selected.Year, selected.Month, 1);
                var to = from.AddMonths(1).AddDays(-1);
                students = await _studentService.GetStudentsForPeriodAsync(SelectedCohort.Id, from, to);
                break;
            }
            case "Year":
            {
                var from = new DateOnly(selected.Year, 1, 1);
                var to = new DateOnly(selected.Year, 12, 31);
                students = await _studentService.GetStudentsForPeriodAsync(SelectedCohort.Id, from, to);
                break;
            }
            default:
            {
                var date = DateOnly.FromDateTime(selected);
                students = await _studentService.GetStudentsForDateAsync(SelectedCohort.Id, date);
                break;
            }
        }

        _logger.LogDebug("[COHORTS] Loaded {Count} students for {Scope} filter", students.Count, SelectedSessionFilterScope);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Students.Clear();
            foreach (var student in students)
                Students.Add(student);

            StatusMessage = $"Showing {students.Count} attendance record(s) for {SelectedSessionFilterScope.ToLowerInvariant()} filter.";
        });

        ExportCsvCommand.NotifyCanExecuteChanged();
    }
}
