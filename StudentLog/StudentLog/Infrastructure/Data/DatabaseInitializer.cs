using Microsoft.Extensions.Logging;
using MySqlConnector;
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
        await using var connection = (MySqlConnection)dbConnection;

        const string createCohortTable = """
            CREATE TABLE IF NOT EXISTS cohort (
                Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                Name VARCHAR(100) NOT NULL
            );
            """;

        const string createStudentTable = """
            CREATE TABLE IF NOT EXISTS student (
                Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                UID VARCHAR(100) NOT NULL UNIQUE,
                cohortId INT NOT NULL,
                SignInTime DATETIME NULL,
                SignOutTime DATETIME NULL,
                name VARCHAR(100) NOT NULL,
                surname VARCHAR(100) NOT NULL,
                CONSTRAINT FK_student_cohort FOREIGN KEY (cohortId) REFERENCES cohort(Id)
            );
            """;

        await using var cohortCommand = new MySqlCommand(createCohortTable, connection);
        await cohortCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var studentCommand = new MySqlCommand(createStudentTable, connection);
        await studentCommand.ExecuteNonQueryAsync(cancellationToken);

        const string createAttendanceTable = """
            CREATE TABLE IF NOT EXISTS attendance (
                Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                StudentId INT NOT NULL,
                SessionDate DATE NOT NULL,
                SignInTime DATETIME NULL,
                SignOutTime DATETIME NULL,
                CONSTRAINT FK_attendance_student FOREIGN KEY (StudentId) REFERENCES student(Id) ON DELETE CASCADE,
                CONSTRAINT UQ_attendance_student_date UNIQUE (StudentId, SessionDate)
            );
            """;

        await using var attendanceCommand = new MySqlCommand(createAttendanceTable, connection);
        await attendanceCommand.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("[DB] Schema initialisation complete");

#if DEBUG
        await SeedTestDataAsync(connection, cancellationToken);
#endif
    }

    private async Task SeedTestDataAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string checkCohortsQuery = "SELECT COUNT(*) FROM cohort;";
        await using var checkCohortsCommand = new MySqlCommand(checkCohortsQuery, connection);
        var cohortCount = Convert.ToInt32(await checkCohortsCommand.ExecuteScalarAsync(cancellationToken));

        bool cohortsJustSeeded = false;
        if (cohortCount == 0)
        {
            _logger.LogInformation("[DB] Seeding test cohorts...");

            const string insertCohortsQuery = """
                INSERT INTO cohort (Name) VALUES
                ('cohort 2026'),
                ('cohort 1');
                """;

            await using var insertCohortsCommand = new MySqlCommand(insertCohortsQuery, connection);
            await insertCohortsCommand.ExecuteNonQueryAsync(cancellationToken);
            cohortsJustSeeded = true;
        }

        if (!cohortsJustSeeded) return;

        const string checkStudentsQuery = "SELECT COUNT(*) FROM student;";
        await using var checkStudentsCommand = new MySqlCommand(checkStudentsQuery, connection);
        var studentCount = Convert.ToInt32(await checkStudentsCommand.ExecuteScalarAsync(cancellationToken));

        if (studentCount == 0)
        {
            _logger.LogInformation("[DB] Seeding test students...");

            const string insertStudentsQuery = """
                INSERT INTO student (UID, cohortId, name, surname, SignInTime, SignOutTime) VALUES
                ('04B9B24A901681', 2, 'John', 'Doe', NULL, NULL),
                ('04F1A24A931ABC', 2, 'Jane', 'Smith', NULL, NULL),
                ('04C2D34B012DEF', 2, 'Bob', 'Johnson', NULL, NULL);
                """;

            await using var insertStudentsCommand = new MySqlCommand(insertStudentsQuery, connection);
            await insertStudentsCommand.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("[DB] Test data seeded successfully");
        }
    }
}
