using StudentLog.Core.Models;

namespace StudentLog.Core.Interfaces.Repositories;

public interface IStudentRepository
{
    Task<IReadOnlyList<Student>> GetAllAsync(int? cohortId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetByCohortAsync(int cohortId, CancellationToken cancellationToken = default);
    Task<Student?> GetByIdAsync(int studentId, CancellationToken cancellationToken = default);
    Task<Student?> GetByUidAsync(string uid, CancellationToken cancellationToken = default);
    Task<int> AddAsync(Student student, CancellationToken cancellationToken = default);
    Task<int> UpdateAttendanceAsync(int studentId, DateTime? signInTime, DateTime? signOutTime, CancellationToken cancellationToken = default);
    Task<int> UpsertDailyAttendanceAsync(int studentId, DateOnly sessionDate, SessionType sessionType, DateTime timestamp, CancellationToken cancellationToken = default);
    Task<int> DeleteAsync(int studentId, CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(Student student, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRecord>> GetAttendanceHistoryAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetByCohortAndDateAsync(int cohortId, DateOnly sessionDate, CancellationToken cancellationToken = default);
}
