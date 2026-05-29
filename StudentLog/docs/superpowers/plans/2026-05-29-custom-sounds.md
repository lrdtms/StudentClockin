# Custom Sound Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hardcoded app-package sounds with user-uploadable sounds configurable at cohort and student levels, with a student → cohort → silent fallback chain.

**Architecture:** Nullable `ClockInSoundPath`/`ClockOutSoundPath` columns are added to both the `cohort` and `student` MySQL tables via idempotent `ALTER TABLE … ADD COLUMN IF NOT EXISTS` statements. A new `ISoundConfigService` handles writing paths; the rebuilt `ISoundService.PlayAsync` resolves the fallback chain at playback time. File upload uses MAUI's `FilePicker` and copies files to `FileSystem.AppDataDirectory/sounds/`.

**Tech Stack:** .NET MAUI 10, CommunityToolkit.Mvvm, Plugin.Maui.Audio 4.0, MySqlConnector 2.4, MySQL `IF NOT EXISTS` DDL

---

## File Map

| Action | Path |
|--------|------|
| Modify | `StudentLog/Core/Models/Cohort.cs` |
| Modify | `StudentLog/Core/Models/Student.cs` |
| Modify | `StudentLog/Infrastructure/Data/DatabaseInitializer.cs` |
| Modify | `StudentLog/Core/Interfaces/Repositories/ICohortRepository.cs` |
| Modify | `StudentLog/Infrastructure/Repositories/CohortRepository.cs` |
| Modify | `StudentLog/Infrastructure/Repositories/StudentRepository.cs` |
| Modify | `StudentLog/Application/Interfaces/ISoundService.cs` |
| Modify | `StudentLog/Infrastructure/Services/SoundService.cs` |
| Create | `StudentLog/Application/Interfaces/ISoundConfigService.cs` |
| Create | `StudentLog/Application/Services/SoundConfigService.cs` |
| Modify | `StudentLog/MauiProgram.cs` |
| Modify | `StudentLog/UI/ViewModels/CheckInSessionViewModel.cs` |
| Modify | `StudentLog/UI/ViewModels/CohortsViewModel.cs` |
| Create | `StudentLog/UI/Converters/PathToFileNameConverter.cs` |
| Modify | `StudentLog/App.xaml` |
| Modify | `StudentLog/UI/Views/CohortsPage.xaml` |
| Modify | `StudentLog/UI/ViewModels/StudentsViewModel.cs` |
| Modify | `StudentLog/UI/Views/StudentsPage.xaml` |

---

## Task 1: Extend domain models with sound path properties

**Files:**
- Modify: `StudentLog/Core/Models/Cohort.cs`
- Modify: `StudentLog/Core/Models/Student.cs`

- [ ] **Step 1: Replace `Cohort.cs` with the extended version**

```csharp
namespace StudentLog.Core.Models;

public class Cohort
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ClockInSoundPath { get; set; }
    public string? ClockOutSoundPath { get; set; }
}
```

- [ ] **Step 2: Replace `Student.cs` with the extended version**

```csharp
namespace StudentLog.Core.Models;

public class Student
{
    public int Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public int CohortId { get; set; }
    public DateTime? SignInTime { get; set; }
    public DateTime? SignOutTime { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string? ClockInSoundPath { get; set; }
    public string? ClockOutSoundPath { get; set; }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add StudentLog/Core/Models/Cohort.cs StudentLog/Core/Models/Student.cs
git commit -m "feat: add ClockInSoundPath/ClockOutSoundPath to Cohort and Student models"
```

---

## Task 2: Add database migration for sound path columns

**Files:**
- Modify: `StudentLog/Infrastructure/Data/DatabaseInitializer.cs`

- [ ] **Step 1: Add ALTER TABLE statements to `InitializeAsync` after the existing attendance table block**

Open `StudentLog/Infrastructure/Data/DatabaseInitializer.cs` and insert this block after the `await attendanceCommand.ExecuteNonQueryAsync` line (around line 58), before the `_logger.LogInformation` line:

```csharp
        const string addCohortSoundColumns = """
            ALTER TABLE cohort
              ADD COLUMN IF NOT EXISTS ClockInSoundPath  VARCHAR(500) NULL,
              ADD COLUMN IF NOT EXISTS ClockOutSoundPath VARCHAR(500) NULL;
            """;

        await using var cohortSoundCommand = new MySqlCommand(addCohortSoundColumns, connection);
        await cohortSoundCommand.ExecuteNonQueryAsync(cancellationToken);

        const string addStudentSoundColumns = """
            ALTER TABLE student
              ADD COLUMN IF NOT EXISTS ClockInSoundPath  VARCHAR(500) NULL,
              ADD COLUMN IF NOT EXISTS ClockOutSoundPath VARCHAR(500) NULL;
            """;

        await using var studentSoundCommand = new MySqlCommand(addStudentSoundColumns, connection);
        await studentSoundCommand.ExecuteNonQueryAsync(cancellationToken);
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Infrastructure/Data/DatabaseInitializer.cs
git commit -m "feat: add ALTER TABLE migrations for sound path columns"
```

---

## Task 3: Add `UpdateAsync` to CohortRepository and update SELECT queries

**Files:**
- Modify: `StudentLog/Core/Interfaces/Repositories/ICohortRepository.cs`
- Modify: `StudentLog/Infrastructure/Repositories/CohortRepository.cs`

- [ ] **Step 1: Add `UpdateAsync` to `ICohortRepository.cs`**

Replace the file contents with:

```csharp
using StudentLog.Core.Models;

namespace StudentLog.Core.Interfaces.Repositories;

public interface ICohortRepository
{
    Task<IReadOnlyList<Cohort>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddAsync(Cohort cohort, CancellationToken cancellationToken = default);
    Task<Cohort?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Cohort cohort, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Replace `CohortRepository.cs` with the updated version**

Replace the full file contents with:

```csharp
using MySqlConnector;
using StudentLog.Core.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Infrastructure.Repositories;

public class CohortRepository : ICohortRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CohortRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => (MySqlConnection)await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

    public async Task<IReadOnlyList<Cohort>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Cohort>();
        await using var connection = await OpenAsync(cancellationToken);

        const string sql = "SELECT Id, Name, ClockInSoundPath, ClockOutSoundPath FROM cohort ORDER BY Name;";
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapCohort(reader));
        }

        return result;
    }

    public async Task<int> AddAsync(Cohort cohort, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "INSERT INTO cohort (Name) VALUES (@name); SELECT LAST_INSERT_ID();";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", cohort.Name);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        cohort.Id = id;
        return id;
    }

    public async Task<Cohort?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "SELECT Id, Name, ClockInSoundPath, ClockOutSoundPath FROM cohort WHERE Id = @id LIMIT 1;";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapCohort(reader);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var deleteStudentsCmd = new MySqlCommand(
            "DELETE FROM student WHERE cohortId = @id;", connection, transaction);
        deleteStudentsCmd.Parameters.AddWithValue("@id", id);
        await deleteStudentsCmd.ExecuteNonQueryAsync(cancellationToken);

        await using var deleteCohortCmd = new MySqlCommand(
            "DELETE FROM cohort WHERE Id = @id;", connection, transaction);
        deleteCohortCmd.Parameters.AddWithValue("@id", id);
        await deleteCohortCmd.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Cohort cohort, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = """
            UPDATE cohort
            SET Name = @name,
                ClockInSoundPath  = @clockIn,
                ClockOutSoundPath = @clockOut
            WHERE Id = @id;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", cohort.Name);
        command.Parameters.AddWithValue("@clockIn", (object?)cohort.ClockInSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@clockOut", (object?)cohort.ClockOutSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", cohort.Id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Cohort MapCohort(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        ClockInSoundPath  = reader.IsDBNull(reader.GetOrdinal("ClockInSoundPath"))  ? null : reader.GetString("ClockInSoundPath"),
        ClockOutSoundPath = reader.IsDBNull(reader.GetOrdinal("ClockOutSoundPath")) ? null : reader.GetString("ClockOutSoundPath"),
    };
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add StudentLog/Core/Interfaces/Repositories/ICohortRepository.cs StudentLog/Infrastructure/Repositories/CohortRepository.cs
git commit -m "feat: add UpdateAsync to CohortRepository, include sound paths in SELECT queries"
```

---

## Task 4: Update StudentRepository to include sound path columns

**Files:**
- Modify: `StudentLog/Infrastructure/Repositories/StudentRepository.cs`

The key changes are: (a) update every `SELECT` that feeds `MapStudent` to include the two new columns, (b) update `MapStudent` to read them, (c) update `AddAsync` and `UpdateAsync` to write them.

- [ ] **Step 1: Replace `StudentRepository.cs` with the updated version**

Replace the full file contents with:

```csharp
using System.Data;
using Microsoft.Extensions.Logging;
using MySqlConnector;
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

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => (MySqlConnection)await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

    public async Task<IReadOnlyList<Student>> GetAllAsync(int? cohortId = null, CancellationToken cancellationToken = default)
    {
        var result = new List<Student>();
        await using var connection = await OpenAsync(cancellationToken);

        var sql = "SELECT Id, UID, cohortId, SignInTime, SignOutTime, name, surname, ClockInSoundPath, ClockOutSoundPath FROM student";
        if (cohortId.HasValue)
            sql += " WHERE cohortId = @cohortId";
        sql += " ORDER BY surname, name;";

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
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
            SELECT LAST_INSERT_ID();
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@uid", student.UID);
        command.Parameters.AddWithValue("@cohortId", student.CohortId);
        command.Parameters.AddWithValue("@signInTime", student.SignInTime);
        command.Parameters.AddWithValue("@signOutTime", student.SignOutTime);
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
                var txConnection = (MySqlConnection)transaction.Connection!;
                var txCast = (MySqlTransaction)transaction;
                await using var txCommand = new MySqlCommand(sql, txConnection, txCast);
                txCommand.Parameters.AddWithValue("@signInTime", (object?)signInTime ?? DBNull.Value);
                txCommand.Parameters.AddWithValue("@signOutTime", (object?)signOutTime ?? DBNull.Value);
                txCommand.Parameters.AddWithValue("@id", studentId);
                var rows = await txCommand.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogDebug("[REPOSITORY] UpdateAttendanceAsync completed. Rows affected: {Rows}", rows);
                return rows;
            }

            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
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
              ON DUPLICATE KEY UPDATE SignInTime = @timestamp, SignOutTime = NULL
              """
            : """
              INSERT INTO attendance (StudentId, SessionDate, SignInTime, SignOutTime)
              VALUES (@studentId, @sessionDate, NULL, @timestamp)
              ON DUPLICATE KEY UPDATE SignOutTime = @timestamp
              """;

        if (transaction is not null)
        {
            var txConnection = (MySqlConnection)transaction.Connection!;
            var txCast = (MySqlTransaction)transaction;
            await using var txCommand = new MySqlCommand(sql, txConnection, txCast);
            txCommand.Parameters.AddWithValue("@studentId", studentId);
            txCommand.Parameters.AddWithValue("@sessionDate", sessionDate.ToDateTime(TimeOnly.MinValue));
            txCommand.Parameters.AddWithValue("@timestamp", timestamp);
            return await txCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@studentId", studentId);
        command.Parameters.AddWithValue("@sessionDate", sessionDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@timestamp", timestamp);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "DELETE FROM student WHERE Id = @id;";

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
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

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@studentId", studentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AttendanceRecord
            {
                StudentId = reader.GetInt32("StudentId"),
                StudentName = reader.GetString("name"),
                StudentSurname = reader.GetString("surname"),
                SignInTime = reader.IsDBNull(reader.GetOrdinal("SignInTime")) ? null : reader.GetDateTime("SignInTime"),
                SignOutTime = reader.IsDBNull(reader.GetOrdinal("SignOutTime")) ? null : reader.GetDateTime("SignOutTime")
            });
        }

        return result;
    }

    private static Student MapStudent(MySqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        UID = reader.GetString("UID"),
        CohortId = reader.GetInt32("cohortId"),
        SignInTime = reader.IsDBNull(reader.GetOrdinal("SignInTime")) ? null : reader.GetDateTime("SignInTime"),
        SignOutTime = reader.IsDBNull(reader.GetOrdinal("SignOutTime")) ? null : reader.GetDateTime("SignOutTime"),
        Name = reader.GetString("name"),
        Surname = reader.GetString("surname"),
        ClockInSoundPath  = reader.IsDBNull(reader.GetOrdinal("ClockInSoundPath"))  ? null : reader.GetString("ClockInSoundPath"),
        ClockOutSoundPath = reader.IsDBNull(reader.GetOrdinal("ClockOutSoundPath")) ? null : reader.GetString("ClockOutSoundPath"),
    };
}
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/Infrastructure/Repositories/StudentRepository.cs
git commit -m "feat: include ClockInSoundPath/ClockOutSoundPath in all student repository queries"
```

---

## Task 5: Redesign ISoundService and rewrite SoundService

**Files:**
- Modify: `StudentLog/Application/Interfaces/ISoundService.cs`
- Modify: `StudentLog/Infrastructure/Services/SoundService.cs`

- [ ] **Step 1: Replace `ISoundService.cs`**

```csharp
using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ISoundService
{
    Task PlayAsync(Student student, Cohort cohort, SessionType sessionType);
    Task<string?> UploadSoundAsync();
}
```

- [ ] **Step 2: Replace `SoundService.cs`**

```csharp
using Plugin.Maui.Audio;
using StudentLog.Application.Interfaces;
using StudentLog.Core.Models;

namespace StudentLog.Infrastructure.Services;

public class SoundService : ISoundService
{
    private readonly IAudioManager _audioManager;

    public SoundService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public async Task PlayAsync(Student student, Cohort cohort, SessionType sessionType)
    {
        var path = sessionType == SessionType.ClockIn
            ? (student.ClockInSoundPath ?? cohort.ClockInSoundPath)
            : (student.ClockOutSoundPath ?? cohort.ClockOutSoundPath);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        await using var stream = File.OpenRead(path);
        var player = _audioManager.CreateAsyncPlayer(stream);
        await player.PlayAsync(CancellationToken.None);
    }

    public async Task<string?> UploadSoundAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Select a sound file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac" } },
                { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "ogg", "flac" } },
            })
        };

        var result = await FilePicker.Default.PickAsync(options);
        if (result is null)
            return null;

        var soundsDir = Path.Combine(FileSystem.AppDataDirectory, "sounds");
        Directory.CreateDirectory(soundsDir);

        var destFileName = $"{Guid.NewGuid():N}-{Path.GetFileName(result.FileName)}";
        var destPath = Path.Combine(soundsDir, destFileName);

        await using var src = await result.OpenReadAsync();
        await using var dst = File.Create(destPath);
        await src.CopyToAsync(dst);

        return destPath;
    }
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded` (compile errors expected in `CheckInSessionViewModel` — fixed in Task 7)

> Note: The build may show errors in `CheckInSessionViewModel.cs` because it still calls the old `PlayClockInAsync`/`PlayClockOutAsync`. This is expected and resolved in Task 7.

- [ ] **Step 4: Commit**

```bash
git add StudentLog/Application/Interfaces/ISoundService.cs StudentLog/Infrastructure/Services/SoundService.cs
git commit -m "feat: redesign ISoundService with PlayAsync fallback chain and UploadSoundAsync"
```

---

## Task 6: Create ISoundConfigService and SoundConfigService, register in DI

**Files:**
- Create: `StudentLog/Application/Interfaces/ISoundConfigService.cs`
- Create: `StudentLog/Application/Services/SoundConfigService.cs`
- Modify: `StudentLog/MauiProgram.cs`

- [ ] **Step 1: Create `ISoundConfigService.cs`**

`ISoundConfigService` is used only for cohort sounds — cohort rows save immediately on button click (no Edit modal). Student sounds are saved via the existing `IStudentService.UpdateStudentAsync` flow when SAVE is clicked in the edit modal, so no separate method is needed here.

```csharp
using StudentLog.Core.Models;

namespace StudentLog.Application.Interfaces;

public interface ISoundConfigService
{
    Task SetCohortSoundAsync(int cohortId, SessionType type, string? path, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create `SoundConfigService.cs`**

```csharp
using StudentLog.Application.Interfaces;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;

namespace StudentLog.Application.Services;

public class SoundConfigService : ISoundConfigService
{
    private readonly ICohortRepository _cohortRepository;

    public SoundConfigService(ICohortRepository cohortRepository)
    {
        _cohortRepository = cohortRepository;
    }

    public async Task SetCohortSoundAsync(int cohortId, SessionType type, string? path, CancellationToken cancellationToken = default)
    {
        var cohort = await _cohortRepository.GetByIdAsync(cohortId, cancellationToken);
        if (cohort is null) return;

        if (type == SessionType.ClockIn)
            cohort.ClockInSoundPath = path;
        else
            cohort.ClockOutSoundPath = path;

        await _cohortRepository.UpdateAsync(cohort, cancellationToken);
    }
}
```

- [ ] **Step 3: Register `SoundConfigService` in `MauiProgram.cs`**

In `MauiProgram.cs`, add the following line directly after the `AddSingleton<ISoundService, SoundService>()` line:

```csharp
builder.Services.AddSingleton<ISoundConfigService, SoundConfigService>();
```

Also add the using at the top of the file (if not already covered by global usings):

```csharp
using StudentLog.Application.Services;
```

- [ ] **Step 4: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded` (compile errors in `CheckInSessionViewModel` still expected — fixed next)

- [ ] **Step 5: Commit**

```bash
git add StudentLog/Application/Interfaces/ISoundConfigService.cs StudentLog/Application/Services/SoundConfigService.cs StudentLog/MauiProgram.cs
git commit -m "feat: add ISoundConfigService and SoundConfigService for writing sound paths"
```

---

## Task 7: Update CheckInSessionViewModel to use new PlayAsync signature

**Files:**
- Modify: `StudentLog/UI/ViewModels/CheckInSessionViewModel.cs`

- [ ] **Step 1: Replace the sound call in the NFC scan callback**

Find these two lines in `CheckInSessionViewModel.cs` (around lines 115-117):

```csharp
_ = SelectedSessionType == SessionType.ClockIn
    ? _soundService.PlayClockInAsync()
    : _soundService.PlayClockOutAsync();
```

Replace them with:

```csharp
_ = _soundService.PlayAsync(result.Student, SelectedCohort!, SelectedSessionType);
```

- [ ] **Step 2: Build to verify (no errors expected now)**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/UI/ViewModels/CheckInSessionViewModel.cs
git commit -m "feat: update CheckInSessionViewModel to use ISoundService.PlayAsync with fallback chain"
```

---

## Task 8: Add PathToFileNameConverter

**Files:**
- Create: `StudentLog/UI/Converters/PathToFileNameConverter.cs`
- Modify: `StudentLog/App.xaml`

- [ ] **Step 1: Create `PathToFileNameConverter.cs`**

```csharp
using System.Globalization;

namespace StudentLog.UI.Converters;

public class PathToFileNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return string.Empty;

        return Path.GetFileName(path);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Register the converter in `App.xaml`**

Add the following line inside the `<ResourceDictionary>` block in `App.xaml`, alongside the existing converters:

```xml
<converters:PathToFileNameConverter x:Key="PathToFileNameConverter" />
```

The converters section should now look like:

```xml
<!-- Converters -->
<converters:StringToBoolConverter x:Key="StringToBoolConverter" />
<converters:SignInStatusConverter x:Key="SignInStatusConverter" />
<converters:PathToFileNameConverter x:Key="PathToFileNameConverter" />
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add StudentLog/UI/Converters/PathToFileNameConverter.cs StudentLog/App.xaml
git commit -m "feat: add PathToFileNameConverter for displaying filenames from full paths"
```

---

## Task 9: Update CohortsViewModel with sound upload commands

**Files:**
- Modify: `StudentLog/UI/ViewModels/CohortsViewModel.cs`

- [ ] **Step 1: Add `ISoundService` and `ISoundConfigService` to the constructor and fields**

Add the two new fields after the existing `_dialogService` field:

```csharp
private readonly ISoundService _soundService;
private readonly ISoundConfigService _soundConfigService;
```

Update the constructor signature to include the new parameters:

```csharp
public CohortsViewModel(
    ICohortService cohortService,
    IStudentService studentService,
    ICsvExportService csvExportService,
    IDialogService dialogService,
    ISoundService soundService,
    ISoundConfigService soundConfigService,
    ILogger<CohortsViewModel> logger)
```

Add the following two lines inside the constructor body, after the `_dialogService = dialogService;` assignment:

```csharp
_soundService = soundService;
_soundConfigService = soundConfigService;
```

Also add the required using statements at the top of the file:

```csharp
using StudentLog.Application.Interfaces;
```

(This namespace likely already exists in the file — check before adding.)

- [ ] **Step 2: Add the two sound relay commands**

Add these two methods to `CohortsViewModel`, after the `DeleteCohortAsync` command:

```csharp
[RelayCommand]
private async Task SetCohortClockInSoundAsync(Cohort cohort)
{
    var path = await _soundService.UploadSoundAsync();
    if (path is null) return;

    await _soundConfigService.SetCohortSoundAsync(cohort.Id, SessionType.ClockIn, path);
    await LoadAsync();
}

[RelayCommand]
private async Task SetCohortClockOutSoundAsync(Cohort cohort)
{
    var path = await _soundService.UploadSoundAsync();
    if (path is null) return;

    await _soundConfigService.SetCohortSoundAsync(cohort.Id, SessionType.ClockOut, path);
    await LoadAsync();
}
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add StudentLog/UI/ViewModels/CohortsViewModel.cs
git commit -m "feat: add SetCohortClockInSoundAsync and SetCohortClockOutSoundAsync to CohortsViewModel"
```

---

## Task 10: Update CohortsPage.xaml with sound buttons on cohort rows

**Files:**
- Modify: `StudentLog/UI/Views/CohortsPage.xaml`

- [ ] **Step 1: Replace the cohort `CollectionView.ItemTemplate` DataTemplate**

Find the `<DataTemplate x:DataType="models:Cohort">` block inside the cohort `CollectionView` (around lines 29-48 in the current file). Replace the entire `<Border>` inside it with:

```xml
<Border Style="{StaticResource Card}" Margin="0,2">
    <Grid RowDefinitions="Auto,Auto" ColumnDefinitions="*,Auto,Auto,Auto" ColumnSpacing="8" RowSpacing="2">

        <!-- Row 0: Name and action buttons -->
        <Label Grid.Row="0" Grid.Column="0"
               Text="{Binding Name}"
               FontFamily="OpenSansSemibold"
               FontSize="14"
               VerticalOptions="Center" />

        <Button Grid.Row="0" Grid.Column="1"
                Text="SOUND IN"
                Style="{StaticResource SecondaryButton}"
                FontSize="10"
                Padding="8,4"
                MinimumHeightRequest="28"
                Command="{Binding Source={RelativeSource AncestorType={x:Type viewModels:CohortsViewModel}}, Path=SetCohortClockInSoundCommand}"
                CommandParameter="{Binding .}" />

        <Button Grid.Row="0" Grid.Column="2"
                Text="SOUND OUT"
                Style="{StaticResource SecondaryButton}"
                FontSize="10"
                Padding="8,4"
                MinimumHeightRequest="28"
                Command="{Binding Source={RelativeSource AncestorType={x:Type viewModels:CohortsViewModel}}, Path=SetCohortClockOutSoundCommand}"
                CommandParameter="{Binding .}" />

        <Button Grid.Row="0" Grid.Column="3"
                Text="DELETE"
                Style="{StaticResource DangerButton}"
                FontSize="11"
                Padding="10,6"
                MinimumHeightRequest="32"
                Command="{Binding Source={RelativeSource AncestorType={x:Type viewModels:CohortsViewModel}}, Path=DeleteCohortCommand}"
                CommandParameter="{Binding .}" />

        <!-- Row 1: Current sound filenames -->
        <StackLayout Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="4"
                     Orientation="Horizontal" Spacing="12">
            <Label Text="{Binding ClockInSoundPath, Converter={StaticResource PathToFileNameConverter}, StringFormat='In: {0}'}"
                   Style="{StaticResource MutedLabel}"
                   FontSize="11"
                   IsVisible="{Binding ClockInSoundPath, Converter={StaticResource StringToBoolConverter}}" />
            <Label Text="{Binding ClockOutSoundPath, Converter={StaticResource PathToFileNameConverter}, StringFormat='Out: {0}'}"
                   Style="{StaticResource MutedLabel}"
                   FontSize="11"
                   IsVisible="{Binding ClockOutSoundPath, Converter={StaticResource StringToBoolConverter}}" />
        </StackLayout>

    </Grid>
</Border>
```

- [ ] **Step 2: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add StudentLog/UI/Views/CohortsPage.xaml
git commit -m "feat: add SOUND IN/OUT buttons and filename labels to cohort rows in CohortsPage"
```

---

## Task 11: Update StudentsViewModel with sound upload commands

**Files:**
- Modify: `StudentLog/UI/ViewModels/StudentsViewModel.cs`

- [ ] **Step 1: Add two new observable properties for the edit modal sound display**

Add these two properties in the `[ObservableProperty]` section, after `_isEditingStudent`:

```csharp
[ObservableProperty]
private string? _editStudentClockInSoundPath;

[ObservableProperty]
private string? _editStudentClockOutSoundPath;
```

- [ ] **Step 2: Add `ISoundService` field and constructor parameter**

Student sounds are saved via `IStudentService.UpdateStudentAsync` when SAVE is clicked — `ISoundConfigService` is not needed here. Only `ISoundService` is needed (for `UploadSoundAsync`).

Add one new private field after `_dialogService`:

```csharp
private readonly ISoundService _soundService;
```

Update the constructor signature:

```csharp
public StudentsViewModel(
    IStudentService studentService,
    ICohortService cohortService,
    INfcService nfcService,
    IDialogService dialogService,
    ISoundService soundService)
```

Add the assignment inside the constructor body after `_dialogService = dialogService;`:

```csharp
_soundService = soundService;
```

- [ ] **Step 3: Populate new properties when the edit modal opens**

In the `EditStudentAsync` method, add these two lines after `IsEditingStudent = true;`:

```csharp
EditStudentClockInSoundPath = student.ClockInSoundPath;
EditStudentClockOutSoundPath = student.ClockOutSoundPath;
```

- [ ] **Step 4: Clear new properties when the edit is cancelled or saved**

In `CancelEdit`, add after the existing clear lines:

```csharp
EditStudentClockInSoundPath = null;
EditStudentClockOutSoundPath = null;
```

In `SaveEditedStudentAsync`, add these two lines before calling `UpdateStudentAsync`:

```csharp
SelectedStudentForEdit.ClockInSoundPath = EditStudentClockInSoundPath;
SelectedStudentForEdit.ClockOutSoundPath = EditStudentClockOutSoundPath;
```

And after `IsEditingStudent = false;` in the success path, add:

```csharp
EditStudentClockInSoundPath = null;
EditStudentClockOutSoundPath = null;
```

- [ ] **Step 5: Add the two sound upload relay commands**

Add these two methods after `CancelEdit`:

```csharp
[RelayCommand]
private async Task SetStudentClockInSoundAsync()
{
    var path = await _soundService.UploadSoundAsync();
    if (path is null) return;

    EditStudentClockInSoundPath = path;
}

[RelayCommand]
private async Task SetStudentClockOutSoundAsync()
{
    var path = await _soundService.UploadSoundAsync();
    if (path is null) return;

    EditStudentClockOutSoundPath = path;
}
```

Note: The path is persisted to the DB when the user clicks SAVE (via `SaveEditedStudentAsync` which calls `_studentService.UpdateStudentAsync(SelectedStudentForEdit)` with the updated model, including the new sound paths set in Step 4). No separate DB write is needed here.

- [ ] **Step 6: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 7: Commit**

```bash
git add StudentLog/UI/ViewModels/StudentsViewModel.cs
git commit -m "feat: add sound upload commands and edit modal sound state to StudentsViewModel"
```

---

## Task 12: Update StudentsPage.xaml edit modal with sound rows

**Files:**
- Modify: `StudentLog/UI/Views/StudentsPage.xaml`

- [ ] **Step 1: Update the edit modal `Border` height and add sound rows**

Find the edit modal `Border` element (around line 91):

```xml
<Border IsVisible="{Binding IsEditingStudent}"
        Style="{StaticResource AccentCard}"
        AbsoluteLayout.LayoutBounds="0.5,0.4,440,300"
        AbsoluteLayout.LayoutFlags="PositionProportional">
```

Change `440,300` to `440,400`:

```xml
<Border IsVisible="{Binding IsEditingStudent}"
        Style="{StaticResource AccentCard}"
        AbsoluteLayout.LayoutBounds="0.5,0.4,440,400"
        AbsoluteLayout.LayoutFlags="PositionProportional">
```

- [ ] **Step 2: Add the two sound rows inside the edit modal `VerticalStackLayout`**

Find the `<Entry Placeholder="NFC UID" Text="{Binding EditStudentUid}" />` line. Add the following two sound rows directly after it, before the `<Grid ColumnDefinitions="*,*"` (the Save/Cancel buttons):

```xml
<!-- Clock-in sound -->
<Grid ColumnDefinitions="*,Auto" ColumnSpacing="8">
    <Label Grid.Column="0"
           Text="{Binding EditStudentClockInSoundPath, Converter={StaticResource PathToFileNameConverter}, TargetNullValue='(no clock-in sound)', FallbackValue='(no clock-in sound)'}"
           Style="{StaticResource MutedLabel}"
           VerticalOptions="Center" />
    <Button Grid.Column="1"
            Text="UPLOAD"
            Style="{StaticResource SecondaryButton}"
            FontSize="11"
            Padding="10,6"
            MinimumHeightRequest="32"
            Command="{Binding SetStudentClockInSoundCommand}" />
</Grid>

<!-- Clock-out sound -->
<Grid ColumnDefinitions="*,Auto" ColumnSpacing="8">
    <Label Grid.Column="0"
           Text="{Binding EditStudentClockOutSoundPath, Converter={StaticResource PathToFileNameConverter}, TargetNullValue='(no clock-out sound)', FallbackValue='(no clock-out sound)'}"
           Style="{StaticResource MutedLabel}"
           VerticalOptions="Center" />
    <Button Grid.Column="1"
            Text="UPLOAD"
            Style="{StaticResource SecondaryButton}"
            FontSize="11"
            Padding="10,6"
            MinimumHeightRequest="32"
            Command="{Binding SetStudentClockOutSoundCommand}" />
</Grid>
```

- [ ] **Step 3: Build to verify**

```powershell
dotnet build StudentLog/StudentLog.csproj -f net10.0-windows10.0.19041.0 2>&1 | Select-String "error|Build succeeded"
```

Expected: `Build succeeded`

- [ ] **Step 4: Manual smoke test**

Launch the app and verify:
1. **Cohorts tab** — each cohort row shows SOUND IN and SOUND OUT buttons; clicking either opens a file picker; after selecting a file, the cohort row shows the filename below the name label.
2. **Students tab** — clicking EDIT on a student opens the modal showing two UPLOAD rows; clicking UPLOAD opens a file picker; the label updates to show the chosen filename; clicking SAVE persists the change; reopening the edit modal shows the saved filename.
3. **Check-In session** — start a session on a cohort that has sounds configured, scan a student NFC tag; confirm the correct sound plays (cohort sound if student has none set, student sound if student has one set, silence if neither is set).

- [ ] **Step 5: Commit**

```bash
git add StudentLog/UI/Views/StudentsPage.xaml
git commit -m "feat: add clock-in/out sound upload rows to student edit modal"
```

---

## Done

All tasks complete. The fallback chain (student → cohort → silent) is implemented in `SoundService.PlayAsync`. Sound paths are stored in MySQL and files live in `FileSystem.AppDataDirectory/sounds/`. Both the Cohorts tab (per-row buttons) and the Students edit modal (upload rows) are wired up.
