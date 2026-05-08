using MySqlConnector;

namespace StudentLog.Infrastructure.Data;

public interface IDbConnectionFactory
{
    Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
