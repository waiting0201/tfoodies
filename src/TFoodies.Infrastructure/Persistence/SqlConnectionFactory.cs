using System.Data;
using Microsoft.Data.SqlClient;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Persistence;

/// <summary>
/// Pooled ADO.NET connection factory for Dapper hot-read paths and atomic code-number SQL.
/// Pooling + MARS are configured on the connection string; the runtime manages the pool.
/// </summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
        => _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
