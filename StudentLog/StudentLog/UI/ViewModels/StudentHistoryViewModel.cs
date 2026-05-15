using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

[QueryProperty(nameof(StudentId), "studentId")]
public partial class StudentHistoryViewModel : ObservableObject
{
    private readonly IStudentService _studentService;
    private readonly ICsvExportService _csvExportService;
    private readonly ILogger<StudentHistoryViewModel> _logger;

    public ObservableCollection<AttendanceRecord> AttendanceHistory { get; } = new();

    [ObservableProperty]
    private Student? _student;

    [ObservableProperty]
    private int _studentId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isExporting;

    public bool IsHistoryEmpty => AttendanceHistory.Count == 0;
    public bool IsHistoryNotEmpty => AttendanceHistory.Count > 0;

    public StudentHistoryViewModel(
        IStudentService studentService,
        ICsvExportService csvExportService,
        ILogger<StudentHistoryViewModel> logger)
    {
        _studentService = studentService;
        _csvExportService = csvExportService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            var student = await _studentService.GetStudentByIdAsync(StudentId);

            if (student is null)
            {
                _logger.LogWarning("[HISTORY] Student not found for ID: {StudentId}", StudentId);
                return;
            }

            Student = student;

            var history = await _studentService.GetAttendanceHistoryAsync(StudentId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                AttendanceHistory.Clear();
                foreach (var record in history)
                {
                    AttendanceHistory.Add(record);
                }

                OnPropertyChanged(nameof(IsHistoryEmpty));
                OnPropertyChanged(nameof(IsHistoryNotEmpty));
                ExportCsvCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HISTORY] Error loading attendance history for StudentId: {StudentId}", StudentId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync()
    {
        try
        {
            IsExporting = true;
            ExportCsvCommand.NotifyCanExecuteChanged();

            var suggestedName = Student is not null
                ? $"{Student.Surname}_{Student.Name}_attendance"
                : "attendance_export";

            await _csvExportService.ExportAttendanceAsync(AttendanceHistory, suggestedName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EXPORT] Error exporting CSV");
        }
        finally
        {
            IsExporting = false;
            ExportCsvCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanExportCsv() => IsHistoryNotEmpty && !IsExporting;

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("../");
    }
}
