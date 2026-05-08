using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ICsvExportService
{
    Task<bool> ExportAttendanceAsync(
        IReadOnlyList<AttendanceRecord> records,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
