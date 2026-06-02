using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using StudentLog.Core.Interfaces;

namespace StudentLog.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dbConnection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var connection = (SqliteConnection)dbConnection;

        const string createCohortTable = """
            CREATE TABLE IF NOT EXISTS cohort (
                Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );
            """;

        const string createStudentTable = """
            CREATE TABLE IF NOT EXISTS student (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                UID         TEXT NOT NULL UNIQUE,
                cohortId    INTEGER NOT NULL,
                SignInTime  TEXT NULL,
                SignOutTime TEXT NULL,
                name        TEXT NOT NULL,
                surname     TEXT NOT NULL,
                FOREIGN KEY (cohortId) REFERENCES cohort(Id)
            );
            """;

        const string createAttendanceTable = """
            CREATE TABLE IF NOT EXISTS attendance (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                StudentId   INTEGER NOT NULL,
                SessionDate TEXT NOT NULL,
                SignInTime  TEXT NULL,
                SignOutTime TEXT NULL,
                FOREIGN KEY (StudentId) REFERENCES student(Id) ON DELETE CASCADE,
                UNIQUE (StudentId, SessionDate)
            );
            """;

        await ExecuteAsync(connection, createCohortTable, cancellationToken);
        await ExecuteAsync(connection, createStudentTable, cancellationToken);
        await ExecuteAsync(connection, createAttendanceTable, cancellationToken);

        await AddColumnIfMissingAsync(connection, "cohort",  "ClockInSoundPath",  "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "cohort",  "ClockOutSoundPath", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "student", "ClockInSoundPath",  "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "student", "ClockOutSoundPath", "TEXT NULL", cancellationToken);

        _logger.LogInformation("[DB] Schema initialisation complete");

#if DEBUG
        await SeedTestDataAsync(connection, cancellationToken);
#endif
    }

    private async Task SeedTestDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var checkCohortsCmd = new SqliteCommand("SELECT COUNT(*) FROM cohort;", connection);
        var cohortCount = Convert.ToInt32(await checkCohortsCmd.ExecuteScalarAsync(cancellationToken));

        if (cohortCount != 0) return;

        _logger.LogInformation("[DB] Seeding test cohorts...");
        const string insertCohorts = """
            INSERT INTO cohort (Name) VALUES ('cohort 2026'), ('cohort 1');
            """;
        await ExecuteAsync(connection, insertCohorts, cancellationToken);

        await using var checkStudentsCmd = new SqliteCommand("SELECT COUNT(*) FROM student;", connection);
        var studentCount = Convert.ToInt32(await checkStudentsCmd.ExecuteScalarAsync(cancellationToken));

        if (studentCount != 0) return;

        _logger.LogInformation("[DB] Seeding test students...");
        const string insertStudents = """
            INSERT INTO student (UID, cohortId, name, surname, SignInTime, SignOutTime) VALUES
            ('04B9B24A901681', 2, 'John',  'Doe',     NULL, NULL),
            ('04F1A24A931ABC', 2, 'Jane',  'Smith',   NULL, NULL),
            ('04C2D34B012DEF', 2, 'Bob',   'Johnson', NULL, NULL);
            """;
        await ExecuteAsync(connection, insertStudents, cancellationToken);
        _logger.LogInformation("[DB] Test data seeded successfully");
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await using var pragmaCmd = new SqliteCommand($"PRAGMA table_info(\"{table}\");", connection);
        await using var reader = await pragmaCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetString(reader.GetOrdinal("name"))
                    .Equals(column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await using var alterCmd = new SqliteCommand(
            $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};", connection);
        await alterCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
