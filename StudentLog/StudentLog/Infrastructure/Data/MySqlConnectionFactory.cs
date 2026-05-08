using MySqlConnector;

namespace StudentLog.Infrastructure.Data;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly MySqlOptions _options;

    public MySqlConnectionFactory(MySqlOptions options)
    {
        _options = options;
    }

    public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
