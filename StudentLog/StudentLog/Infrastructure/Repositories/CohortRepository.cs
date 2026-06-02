using Microsoft.Data.Sqlite;
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

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
        => (SqliteConnection)await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

    public async Task<IReadOnlyList<Cohort>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Cohort>();
        await using var connection = await OpenAsync(cancellationToken);

        const string sql = "SELECT Id, Name, ClockInSoundPath, ClockOutSoundPath FROM cohort ORDER BY Name;";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            result.Add(MapCohort(reader));

        return result;
    }

    public async Task<int> AddAsync(Cohort cohort, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "INSERT INTO cohort (Name) VALUES (@name); SELECT last_insert_rowid();";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", cohort.Name);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        cohort.Id = id;
        return id;
    }

    public async Task<Cohort?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string sql = "SELECT Id, Name, ClockInSoundPath, ClockOutSoundPath FROM cohort WHERE Id = @id LIMIT 1;";

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapCohort(reader);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var deleteStudentsCmd = new SqliteCommand(
            "DELETE FROM student WHERE cohortId = @id;", connection, transaction);
        deleteStudentsCmd.Parameters.AddWithValue("@id", id);
        await deleteStudentsCmd.ExecuteNonQueryAsync(cancellationToken);

        await using var deleteCohortCmd = new SqliteCommand(
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

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", cohort.Name);
        command.Parameters.AddWithValue("@clockIn", (object?)cohort.ClockInSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@clockOut", (object?)cohort.ClockOutSoundPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", cohort.Id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Cohort MapCohort(SqliteDataReader reader)
    {
        int idxId       = reader.GetOrdinal("Id");
        int idxName     = reader.GetOrdinal("Name");
        int idxClockIn  = reader.GetOrdinal("ClockInSoundPath");
        int idxClockOut = reader.GetOrdinal("ClockOutSoundPath");

        return new Cohort
        {
            Id                = reader.GetInt32(idxId),
            Name              = reader.GetString(idxName),
            ClockInSoundPath  = reader.IsDBNull(idxClockIn)  ? null : reader.GetString(idxClockIn),
            ClockOutSoundPath = reader.IsDBNull(idxClockOut) ? null : reader.GetString(idxClockOut),
        };
    }
}
