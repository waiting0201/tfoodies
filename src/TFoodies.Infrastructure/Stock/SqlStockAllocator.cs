using System.Data;
using Dapper;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Inventory;

/// <summary>
/// FIFO-by-expiry 庫存揀貨。
///
/// 移植舊系統 WarehouseStocksService.GetStockWarehouses + Librarys.CheckInventory/SetInventory 的邏輯：
///   1. 以 UPDLOCK + ROWLOCK 按效期 ASC 鎖定同倉庫 + 同品項的 Warehousestocks 列，防止並發超賣。
///   2. 從效期最舊的批次開始消耗 quantity_left，直到滿足所需數量。
///   3. 每消耗一個批次 UPDATE 其 quantity_left，回傳 StockPick 供 Orderdetailstocks 寫入。
///   4. 若庫存不足，回傳 AllocationResult.Insufficient（不修改任何列）。
///
/// 呼叫端必須提供一個已開啟的 IDbTransaction，確保揀貨與下單在同一個 tx 中（all-or-nothing）。
/// </summary>
public sealed class SqlStockAllocator : IStockAllocator
{
    public async Task<AllocationResult> AllocateAsync(
        Guid warehouseId,
        Guid productId,
        int quantity,
        IDbTransaction transaction,
        CancellationToken ct = default)
    {
        if (quantity <= 0) return AllocationResult.Insufficient;

        var conn = transaction.Connection!;

        // 找到同倉 + 同商品的所有批次（透過 Stocks），效期 ASC（FIFO），只取 quantity_left > 0 的列。
        // UPDLOCK + ROWLOCK：讀取時即上排他鎖，阻止其他 tx 讀到相同列後各自扣減。
        const string selectSql = @"
SELECT
    ws.warehousestockid,
    ws.stockid,
    ws.quantity_left,
    s.expiredate
FROM Warehousestocks ws WITH (UPDLOCK, ROWLOCK)
JOIN Stocks s ON s.stockid = ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid = s.purchasedetailid
WHERE ws.warehouseid = @warehouseId
  AND pd.productid = @productId
  AND ws.quantity_left > 0
ORDER BY s.expiredate ASC, ws.transdate ASC;";

        var batches = (await conn.QueryAsync<BatchRow>(
            selectSql,
            new { warehouseId, productId },
            transaction)).ToList();

        // 先確認總量充足（避免部分更新後再 rollback）
        var total = batches.Sum(b => b.quantity_left);
        if (total < quantity) return AllocationResult.Insufficient;

        var picks = new List<StockPick>();
        var remaining = quantity;

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;

            var take = Math.Min(remaining, batch.quantity_left);
            var newLeft = batch.quantity_left - take;

            await conn.ExecuteAsync(
                "UPDATE Warehousestocks SET quantity_left = @newLeft WHERE warehousestockid = @id",
                new { newLeft, id = batch.warehousestockid },
                transaction);

            picks.Add(new StockPick(batch.warehousestockid, batch.stockid, take,
                batch.expiredate.HasValue
                    ? new DateTime(batch.expiredate.Value.Year, batch.expiredate.Value.Month, batch.expiredate.Value.Day, 0, 0, 0, DateTimeKind.Unspecified)
                    : null));

            remaining -= take;
        }

        return new AllocationResult(true, picks);
    }

    private sealed record BatchRow(Guid warehousestockid, Guid stockid, int quantity_left, DateOnly? expiredate);
}
