using System.Data;
using Microsoft.Extensions.Options;
using MySqlConnector;
using StudentLog.Core.Interfaces;

namespace StudentLog.Infrastructure.Data;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly MySqlOptions _options;

    public MySqlConnectionFactory(IOptions<MySqlOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
