using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;
using StudentLog.UI.Messaging;
using System.Collections.ObjectModel;

namespace StudentLog.UI.ViewModels;

public partial class CheckInSessionViewModel : ObservableObject
{
    private readonly ICohortService _cohortService;
    private readonly ISessionStateService _sessionStateService;
    private readonly INfcService _nfcService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILogger<CheckInSessionViewModel> _logger;

    public ObservableCollection<Cohort> Cohorts { get; } = new();
    public IReadOnlyList<SessionType> SessionTypes { get; } = new[] { SessionType.ClockIn, SessionType.ClockOut };

    [ObservableProperty]
    private Cohort? _selectedCohort;

    [ObservableProperty]
    private SessionType _selectedSessionType = SessionType.ClockIn;

    [ObservableProperty]
    private bool _isSessionActive;

    [ObservableProperty]
    private string _statusMessage = "Select cohort and start a session.";

    [ObservableProperty]
    private string _lastScannedStudentName = string.Empty;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    public CheckInSessionViewModel(
        ICohortService cohortService,
        ISessionStateService sessionStateService,
        INfcService nfcService,
        IAttendanceService attendanceService,
        ILogger<CheckInSessionViewModel> logger)
    {
        _cohortService = cohortService;
        _sessionStateService = sessionStateService;
        _nfcService = nfcService;
        _attendanceService = attendanceService;
        _logger = logger;
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
    public async Task StartSessionAsync()
    {
        try
        {
            if (SelectedCohort is null)
            {
                StatusMessage = "Please select a cohort.";
                return;
            }

            _logger.LogInformation("[VM] Starting session for cohort: {Cohort}, type: {Type}",
                SelectedCohort.Name, SelectedSessionType);

            if (IsSessionActive)
            {
                await _nfcService.StopListeningAsync();
            }

            _sessionStateService.StartSession(SelectedCohort.Id, SelectedSessionType, DateOnly.FromDateTime(SelectedDate));
            IsSessionActive = true;
            StatusMessage = $"{SelectedSessionType} session started for {SelectedCohort.Name}. Listening for scans...";
            LastScannedStudentName = string.Empty;

            _logger.LogInformation("[VM] Session started. Listening for NFC scans...");

            await _nfcService.StartListeningAsync(async uid =>
            {
                try
                {
                    _logger.LogDebug("[VM] NFC scan received: {Uid}", uid);

                    var result = await _attendanceService.RecordScanAsync(SelectedSessionType, uid);

                    _logger.LogDebug("[VM] RecordScanAsync result - Success: {Success}, Message: {Message}",
                        result.IsSuccess, result.Message);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (result.IsSuccess && result.Student is not null)
                        {
                            StatusMessage = $"Recorded {SelectedSessionType} for {result.Student.Name} {result.Student.Surname}.";
                            LastScannedStudentName = $"Scanned: {result.Student.Name} {result.Student.Surname}";
                            _logger.LogInformation("[VM] Attendance recorded for {Name} {Surname}",
                                result.Student.Name, result.Student.Surname);
                            WeakReferenceMessenger.Default.Send(new AttendanceRecordedMessage(result.Student.CohortId, result.Timestamp ?? DateTime.Now));
                            return;
                        }

                        StatusMessage = result.Message;
                        LastScannedStudentName = string.Empty;
                        _logger.LogWarning("[VM] Attendance recording failed: {Message}", result.Message);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[VM] Exception during NFC scan processing");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusMessage = $"Error processing scan: {ex.Message}";
                        LastScannedStudentName = string.Empty;
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VM] Exception in StartSessionAsync");
            IsSessionActive = false;
            StatusMessage = $"Error starting session: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StopSessionAsync()
    {
        try
        {
            _logger.LogInformation("[VM] Stopping session");
            await _nfcService.StopListeningAsync();
            _sessionStateService.StopSession();
            IsSessionActive = false;
            StatusMessage = "Session stopped.";
            _logger.LogInformation("[VM] Session stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VM] Exception in StopSessionAsync");
            IsSessionActive = false;
            StatusMessage = $"Error stopping session: {ex.Message}";
        }
    }
}
