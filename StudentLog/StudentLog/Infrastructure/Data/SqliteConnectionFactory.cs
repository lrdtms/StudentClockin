using System.Data;
using Microsoft.Data.Sqlite;
using StudentLog.Core.Interfaces;

namespace StudentLog.Infrastructure.Data;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private static readonly string DbPath = Path.Combine(
        FileSystem.AppDataDirectory, "studentlog.db");

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection($"Data Source={DbPath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
