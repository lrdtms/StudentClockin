using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<Student>> GetStudentsAsync(int? cohortId = null, CancellationToken cancellationToken = default);
    Task<Student?> GetStudentByIdAsync(int studentId, CancellationToken cancellationToken = default);
    Task<int> AddStudentAsync(string name, string surname, string uid, int cohortId, CancellationToken cancellationToken = default);
    Task<int> DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default);
    Task<int> UpdateStudentAsync(Student student, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRecord>> GetAttendanceHistoryAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetStudentsForDateAsync(int cohortId, DateOnly sessionDate, CancellationToken cancellationToken = default);
}
