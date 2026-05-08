using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

[QueryProperty(nameof(StudentId), "studentId")]
public class StudentHistoryViewModel : ObservableObject
{
    private readonly IStudentService _studentService;
    private readonly ICsvExportService _csvExportService;

    public ObservableCollection<AttendanceRecord> AttendanceHistory { get; } = new();

    private Student? _student;
    public Student? Student
    {
        get => _student;
        set => SetProperty(ref _student, value);
    }

    private int _studentId;
    public int StudentId
    {
        get => _studentId;
        set => SetProperty(ref _studentId, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _isExporting;
    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    public bool IsHistoryEmpty => AttendanceHistory.Count == 0;
    public bool IsHistoryNotEmpty => AttendanceHistory.Count > 0;

    public IAsyncRelayCommand BackCommand { get; }
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }

    public StudentHistoryViewModel(IStudentService studentService, ICsvExportService csvExportService)
    {
        _studentService = studentService;
        _csvExportService = csvExportService;
        BackCommand = new AsyncRelayCommand(BackAsync);
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, CanExportCsv);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            var student = await _studentService.GetStudentByIdAsync(StudentId);

            if (student is null)
            {
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
            System.Diagnostics.Debug.WriteLine($"[HISTORY] Error loading attendance history: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanExportCsv() => IsHistoryNotEmpty && !IsExporting;

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
            System.Diagnostics.Debug.WriteLine($"[EXPORT] Error exporting CSV: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
            ExportCsvCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("../");
    }
}
