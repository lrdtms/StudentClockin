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

        const string sql = "SELECT Id, Name FROM cohort ORDER BY Name;";
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Cohort
            {
                Id = reader.GetInt32("Id"),
                Name = reader.GetString("Name")
            });
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
        const string sql = "SELECT Id, Name FROM cohort WHERE Id = @id LIMIT 1;";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Cohort
        {
            Id = reader.GetInt32("Id"),
            Name = reader.GetString("Name")
        };
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
}
