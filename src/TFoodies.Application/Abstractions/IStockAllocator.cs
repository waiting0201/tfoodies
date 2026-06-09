using System.Data;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// FIFO-by-expiry stock allocation. Re-homes the legacy logic that lived in
/// WarehouseStocksService.GetStockWarehouses + Librarys.CheckInventory/SetInventory and was
/// orchestrated in controllers: select Warehousestocks ordered by Stocks.expiredate ASC,
/// decrement quantity_left, write one Orderdetailstock per consumed batch. Rows are taken
/// WITH (UPDLOCK, ROWLOCK) inside the caller's transaction so two concurrent orders cannot
/// over-allocate the same batch.
/// </summary>
public interface IStockAllocator
{
    Task<AllocationResult> AllocateAsync(
        Guid warehouseId,
        Guid productId,
        int quantity,
        IDbTransaction transaction,
        CancellationToken ct = default);
}

/// <summary>Outcome of an allocation attempt; <see cref="Picks"/> is empty when insufficient.</summary>
public sealed record AllocationResult(bool IsSufficient, IReadOnlyList<StockPick> Picks)
{
    public static AllocationResult Insufficient { get; } = new(false, Array.Empty<StockPick>());
}

/// <summary>One batch consumed: which warehousestock row and how much was taken from it.</summary>
public sealed record StockPick(Guid WarehouseStockId, Guid StockId, int Quantity, DateTime? ExpireDate);
