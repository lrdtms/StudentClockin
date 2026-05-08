using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceScanResult> RecordScanAsync(SessionType sessionType, string uid, CancellationToken cancellationToken = default);
}
