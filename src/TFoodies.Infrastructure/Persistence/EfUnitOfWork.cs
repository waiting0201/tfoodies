using Microsoft.EntityFrameworkCore.Storage;
using TFoodies.Application.Abstractions;
using TFoodies.Infrastructure.Persistence;

namespace TFoodies.Infrastructure.Persistence;

/// <summary>
/// EF Core 實作的工作單元。DbContext 為 Scoped（每次 Function 呼叫一個實例），
/// 提供 SaveChangesAsync 與明確交易邊界。Dapper 呼叫透過 DbContext.Database.GetDbConnection()
/// 取得相同底層連線，可在同一 transaction 內混合 EF + Dapper 寫入。
/// </summary>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly TfoodiesContext _db;

    public EfUnitOfWork(TfoodiesContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new EfTransaction(tx);
    }

    private sealed class EfTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _tx;

        public EfTransaction(IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken ct = default) => _tx.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => _tx.RollbackAsync(ct);

        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
