namespace TFoodies.Application.Abstractions;

/// <summary>
/// Explicit unit of work / transaction boundary. Replaces the legacy implicit pattern
/// where a shared DbContext was new'd in a controller constructor and committed once per
/// action. In the serverless model the DbContext is Scoped (one per Function invocation);
/// a command handler opens a transaction, does all its writes, then commits once.
/// The <see cref="Connection"/> is exposed so Dapper code-number / FIFO SQL can enlist in
/// the SAME transaction as the EF Core writes.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>An active transaction; commit on success, dispose to roll back.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
