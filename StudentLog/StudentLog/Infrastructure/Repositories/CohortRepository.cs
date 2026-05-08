using MySqlConnector;
using StudentLog.Core.Interfaces.Repositories;
using StudentLog.Core.Models;
using StudentLog.Infrastructure.Data;

namespace StudentLog.Infrastructure.Repositories;

public class CohortRepository : ICohortRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CohortRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Cohort>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<Cohort>();
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

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
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = "INSERT INTO cohort (Name) VALUES (@name); SELECT LAST_INSERT_ID();";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@name", cohort.Name);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        cohort.Id = id;
        return id;
    }

    public async Task<Cohort?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
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
}
