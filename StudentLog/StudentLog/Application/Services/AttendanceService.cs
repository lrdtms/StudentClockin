using Microsoft.Extensions.Logging;
using MySqlConnector;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IStudentRepository _studentRepository;
    private readonly ISessionStateService _sessionStateService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IStudentRepository studentRepository,
        ISessionStateService sessionStateService,
        IDbConnectionFactory connectionFactory,
        ILogger<AttendanceService> logger)
    {
        _studentRepository = studentRepository;
        _sessionStateService = sessionStateService;
        _connectionFactory = connectionFactory;
        _logger = logger;
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

            var sessionDate = DateOnly.FromDateTime(timestamp);

            _logger.LogInformation("[ATTENDANCE] Recording {SessionType} for Student ID: {StudentId}", sessionType, student.Id);

            // Open one connection and wrap both writes in a transaction
            var dbConnection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await using var connection = (MySqlConnection)dbConnection;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                if (sessionType == SessionType.ClockIn)
                {
                    var rows = await _studentRepository.UpdateAttendanceAsync(
                        student.Id, timestamp, student.SignOutTime, cancellationToken, transaction);
                    if (rows == 0)
                        _logger.LogWarning("[ATTENDANCE] ClockIn: no rows updated for Student ID: {StudentId}", student.Id);
                    else
                        _logger.LogDebug("[ATTENDANCE] ClockIn update: {Rows} row(s) affected", rows);
                }
                else
                {
                    var rows = await _studentRepository.UpdateAttendanceAsync(
                        student.Id, student.SignInTime, timestamp, cancellationToken, transaction);
                    if (rows == 0)
                        _logger.LogWarning("[ATTENDANCE] ClockOut: no rows updated for Student ID: {StudentId}", student.Id);
                    else
                        _logger.LogDebug("[ATTENDANCE] ClockOut update: {Rows} row(s) affected", rows);
                }

                await _studentRepository.UpsertDailyAttendanceAsync(
                    student.Id, sessionDate, sessionType, timestamp, cancellationToken, transaction);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            // Update student in memory — no third DB round-trip needed
            if (sessionType == SessionType.ClockIn)
                student.SignInTime = timestamp;
            else
                student.SignOutTime = timestamp;

            _logger.LogInformation("[ATTENDANCE] {SessionType}: {Name} {Surname} at {Timestamp}",
                sessionType, student.Name, student.Surname, timestamp);

            return AttendanceScanResult.Recorded(student, sessionType, timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ATTENDANCE] Exception in RecordScanAsync for UID: {Uid}", uid);
            return AttendanceScanResult.Error($"Error recording attendance: {ex.Message}");
        }
    }
}
