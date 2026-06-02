using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StudentLog.Core.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<StudentRepository> _logger;

    public StudentRepository(IDbConnectionFactory connectionFactory, ILogger<StudentRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
        => (SqliteConnection)await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

    public async Task<IReadOnlyList<Student>> GetAllAsync(int? cohortId = null, CancellationToken cancellationToken = default)
    {
        var result = new List<Student>();
        await using var connection = await OpenAsync(cancellationToken);

        var sql = "SELECT Id, UID, cohortId, SignInTime, SignOutTime, name, surname, ClockInSoundPath, ClockOutSoundPath FROM student";
        if (cohortId.HasValue)
            sql += " WHERE cohortId = @cohortId";
        sql += " ORDER BY surname, name;";

        await using var command = new SqliteCommand(sql, connection);
        if (cohortId.HasValue)
            command.Parameters.AddWithValue("@cohortId", cohortId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(MapStudent(reader));

        return result;
    }

    public async Task<IReadOnlyList<Student>> GetByCohortAsync(int cohortId, CancellationToken cancellationToken = default)
        => await GetAllAsync(cohortId, cancellationToken);

    public async Task<Student?> GetByIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT Id, UID, cohortId, SignInTime, SignOutTime, name, surname, ClockInSoundPath, ClockOutSoundPath
            FROM student WHERE Id = @id LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", studentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapStudent(reader);
    }

    public async Task<Student?> GetByUidAsync(string uid, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            SELECT Id, UID, cohortId, SignInTime, SignOutTime, name, surname, ClockInSoundPath, ClockOutSoundPath
            FROM student WHERE UID = @uid LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", uid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapStudent(reader);
    }

    public async Task<int> AddAsync(Student student, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO student (UID, cohortId, SignInTime, SignOutTime, name, surname, ClockInSoundPath, ClockOutSoundPath)
            VALUES (@uid, @cohortId, @signInTime, @signOutTime, @name, @surname, @clockIn, @clockOut);
            SELECT last_insert_rowid();
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", student.UID);
        command.Parameters.AddWithValue("@cohortId", student.CohortId);
        command.Parameters.AddWithValue("@signInTime", (object?)student.SignInTime ?? DBNull.Value);
        command.Parameters.AddWithValue("@signOutTime", (object?)student.SignOutTime ?? DBNull.Value);
        command.Parameters.AddWithValue("@name", student.Name);
        command.Parameters.AddWithValue("@surname", student.Surname);
        command.Parameters.AddWithValue("@clockIn", (object?)student.ClockInSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@clockOut", (object?)student.ClockOutSoundPath ?? DBNull.Value);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        student.Id = id;
        return id;
    }

    public async Task<int> UpdateAttendanceAsync(
        int studentId,
        DateTime? signInTime,
        DateTime? signOutTime,
        CancellationToken cancellationToken = default,
        IDbTransaction? transaction = null)
    {
        try
        {
            const string sql = "UPDATE student SET SignInTime = @signInTime, SignOutTime = @signOutTime WHERE Id = @id;";

            _logger.LogDebug("[REPOSITORY] UpdateAttendanceAsync for StudentId: {StudentId}, SignInTime: {SignIn}, SignOutTime: {SignOut}",
                studentId, signInTime, signOutTime);

            if (transaction is not null)
            {
                var txConnection = (SqliteConnection)transaction.Connection!;
                var txCast = (SqliteTransaction)transaction;
                await using var txCommand = new SqliteCommand(sql, txConnection, txCast);
                txCommand.Parameters.AddWithValue("@signInTime", (object?)signInTime ?? DBNull.Value);
                txCommand.Parameters.AddWithValue("@signOutTime", (object?)signOutTime ?? DBNull.Value);
                txCommand.Parameters.AddWithValue("@id", studentId);
                var rows = await txCommand.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogDebug("[REPOSITORY] UpdateAttendanceAsync completed. Rows affected: {Rows}", rows);
                return rows;
            }

            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@signInTime", (object?)signInTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@signOutTime", (object?)signOutTime ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", studentId);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogDebug("[REPOSITORY] UpdateAttendanceAsync completed. Rows affected: {Rows}", rowsAffected);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPOSITORY] Exception in UpdateAttendanceAsync for StudentId: {StudentId}", studentId);
            throw;
        }
    }

    public async Task<int> UpsertDailyAttendanceAsync(
        int studentId,
        DateOnly sessionDate,
        SessionType sessionType,
        DateTime timestamp,
        CancellationToken cancellationToken = default,
        IDbTransaction? transaction = null)
    {
        string sql = sessionType == SessionType.ClockIn
            ? """
              INSERT INTO attendance (StudentId, SessionDate, SignInTime, SignOutTime)
              VALUES (@studentId, @sessionDate, @timestamp, NULL)
              ON CONFLICT(StudentId, SessionDate) DO UPDATE SET SignInTime = @timestamp, SignOutTime = NULL
              """
            : """
              INSERT INTO attendance (StudentId, SessionDate, SignInTime, SignOutTime)
              VALUES (@studentId, @sessionDate, NULL, @timestamp)
              ON CONFLICT(StudentId, SessionDate) DO UPDATE SET SignOutTime = @timestamp
              """;

        if (transaction is not null)
        {
            var txConnection = (SqliteConnection)transaction.Connection!;
            var txCast = (SqliteTransaction)transaction;
            await using var txCommand = new SqliteCommand(sql, txConnection, txCast);
            txCommand.Parameters.AddWithValue("@studentId", studentId);
            txCommand.Parameters.AddWithValue("@sessionDate", sessionDate.ToDateTime(TimeOnly.MinValue));
            txCommand.Parameters.AddWithValue("@timestamp", timestamp);
            return await txCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@sessionDate", sessionDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@timestamp", timestamp);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "DELETE FROM student WHERE Id = @id;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", studentId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> UpdateAsync(Student student, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            UPDATE student
            SET UID = @uid, name = @name, surname = @surname, cohortId = @cohortId,
                ClockInSoundPath = @clockIn, ClockOutSoundPath = @clockOut
            WHERE Id = @id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", student.UID);
        command.Parameters.AddWithValue("@name", student.Name);
        command.Parameters.AddWithValue("@surname", student.Surname);
        command.Parameters.AddWithValue("@cohortId", student.CohortId);
        command.Parameters.AddWithValue("@clockIn", (object?)student.ClockInSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@clockOut", (object?)student.ClockOutSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", student.Id);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Student>> GetByCohortAndDateAsync(int cohortId, DateOnly sessionDate, CancellationToken cancellationToken = default)
    {
        var result = new List<Student>();
        await using var connection = await OpenAsync(cancellationToken);

        const string sql = """
            SELECT s.Id, s.UID, s.cohortId, s.name, s.surname,
                   s.ClockInSoundPath, s.ClockOutSoundPath,
                   a.SignInTime, a.SignOutTime
            FROM attendance a
            INNER JOIN student s ON s.Id = a.StudentId
            WHERE s.cohortId = @cohortId AND DATE(a.SessionDate) = DATE(@sessionDate)
            ORDER BY s.surname, s.name;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@cohortId", cohortId);
        command.Parameters.AddWithValue("@sessionDate", sessionDate.ToDateTime(TimeOnly.MinValue));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(MapStudent(reader));

        return result;
    }

    public async Task<IReadOnlyList<Student>> GetByCohortAndPeriodAsync(int cohortId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = new List<Student>();
        await using var connection = await OpenAsync(cancellationToken);

        const string sql = """
            SELECT DISTINCT s.Id, s.UID, s.cohortId, s.name, s.surname,
                   s.ClockInSoundPath, s.ClockOutSoundPath,
                   a.SignInTime, a.SignOutTime
            FROM student s
            INNER JOIN attendance a ON a.StudentId = s.Id
            WHERE s.cohortId = @cohortId
              AND a.SessionDate BETWEEN @from AND @to
            ORDER BY s.surname, s.name;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@cohortId", cohortId);
        command.Parameters.AddWithValue("@from", from.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@to", to.ToDateTime(TimeOnly.MinValue));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(MapStudent(reader));

        return result;
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetAttendanceHistoryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var result = new List<AttendanceRecord>();
        await using var connection = await OpenAsync(cancellationToken);

        const string sql = """
            SELECT a.StudentId, s.name, s.surname, a.SignInTime, a.SignOutTime
            FROM attendance a
            INNER JOIN student s ON s.Id = a.StudentId
            WHERE a.StudentId = @studentId
            ORDER BY a.SessionDate DESC;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@studentId", studentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int idxSignIn  = reader.GetOrdinal("SignInTime");
            int idxSignOut = reader.GetOrdinal("SignOutTime");

            result.Add(new AttendanceRecord
            {
                StudentId      = reader.GetInt32(reader.GetOrdinal("StudentId")),
                StudentName    = reader.GetString(reader.GetOrdinal("name")),
                StudentSurname = reader.GetString(reader.GetOrdinal("surname")),
                SignInTime     = reader.IsDBNull(idxSignIn)  ? null : reader.GetDateTime(idxSignIn),
                SignOutTime    = reader.IsDBNull(idxSignOut) ? null : reader.GetDateTime(idxSignOut),
            });
        }

        return result;
    }

    private static Student MapStudent(SqliteDataReader reader)
    {
        int idxId       = reader.GetOrdinal("Id");
        int idxUid      = reader.GetOrdinal("UID");
        int idxCohort   = reader.GetOrdinal("cohortId");
        int idxSignIn   = reader.GetOrdinal("SignInTime");
        int idxSignOut  = reader.GetOrdinal("SignOutTime");
        int idxName     = reader.GetOrdinal("name");
        int idxSurname  = reader.GetOrdinal("surname");
        int idxClockIn  = reader.GetOrdinal("ClockInSoundPath");
        int idxClockOut = reader.GetOrdinal("ClockOutSoundPath");

        return new Student
        {
            Id                = reader.GetInt32(idxId),
            UID               = reader.GetString(idxUid),
            CohortId          = reader.GetInt32(idxCohort),
            SignInTime        = reader.IsDBNull(idxSignIn)   ? null : reader.GetDateTime(idxSignIn),
            SignOutTime       = reader.IsDBNull(idxSignOut)  ? null : reader.GetDateTime(idxSignOut),
            Name              = reader.GetString(idxName),
            Surname           = reader.GetString(idxSurname),
            ClockInSoundPath  = reader.IsDBNull(idxClockIn)  ? null : reader.GetString(idxClockIn),
            ClockOutSoundPath = reader.IsDBNull(idxClockOut) ? null : reader.GetString(idxClockOut),
        };
    }
}
