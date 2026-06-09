using System.Data;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// Creates open ADO.NET connections for the Dapper hot-read paths (product search,
/// reports, FIFO pick lists) and for the atomic code-number SQL. Pooling is handled by
/// the SqlClient connection string; callers dispose the connection.
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
