using StudentLog.Application.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IStudentRepository _studentRepository;
    private readonly ISessionStateService _sessionStateService;

    public AttendanceService(IStudentRepository studentRepository, ISessionStateService sessionStateService)
    {
        _studentRepository = studentRepository;
        _sessionStateService = sessionStateService;
    }

    public async Task<AttendanceScanResult> RecordScanAsync(SessionType sessionType, string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_sessionStateService.IsSessionActive || !_sessionStateService.ActiveCohortId.HasValue)
            {
                return AttendanceScanResult.SessionInactive();
            }

            var normalizedUid = uid.Trim().ToUpperInvariant();
            var student = await _studentRepository.GetByUidAsync(normalizedUid, cancellationToken);

            if (student is null || student.CohortId != _sessionStateService.ActiveCohortId.Value)
            {
                return AttendanceScanResult.NotInCohort(normalizedUid);
            }

            var now = DateTime.Now;
            var timestamp = _sessionStateService.ActiveDay.HasValue
                ? _sessionStateService.ActiveDay.Value.ToDateTime(TimeOnly.FromDateTime(now))
                : now;

            System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] Updating {sessionType} for Student ID: {student.Id}");

            if (sessionType == SessionType.ClockIn)
            {
                var rowsUpdated = await _studentRepository.UpdateAttendanceAsync(student.Id, timestamp, student.SignOutTime, cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] ClockIn update: {rowsUpdated} row(s) affected");

                if (rowsUpdated == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] WARNING: No rows updated for Student ID: {student.Id}");
                }
            }
            else
            {
                var rowsUpdated = await _studentRepository.UpdateAttendanceAsync(student.Id, student.SignInTime, timestamp, cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] ClockOut update: {rowsUpdated} row(s) affected");

                if (rowsUpdated == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] WARNING: No rows updated for Student ID: {student.Id}");
                }
            }

            var sessionDate = DateOnly.FromDateTime(timestamp);
            await _studentRepository.UpsertDailyAttendanceAsync(student.Id, sessionDate, sessionType, timestamp, cancellationToken);

            var refreshedStudent = await _studentRepository.GetByUidAsync(normalizedUid, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[ATTENDANCE] {sessionType}: {student.Name} {student.Surname} - SignInTime: {refreshedStudent?.SignInTime}, SignOutTime: {refreshedStudent?.SignOutTime}");

            return refreshedStudent is null
                ? AttendanceScanResult.Recorded(student, sessionType, timestamp)
                : AttendanceScanResult.Recorded(refreshedStudent, sessionType, timestamp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ATTENDANCE ERROR] Exception in RecordScanAsync: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ATTENDANCE ERROR] Stack Trace: {ex.StackTrace}");
            return AttendanceScanResult.Error($"Error recording attendance: {ex.Message}");
        }
    }
}
