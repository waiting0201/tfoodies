using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台庫存與倉庫管理。
///   GET  /admin/warehouses               — 倉庫列表
///   GET  /admin/inventory                — 各商品庫存現況（分頁）
///   GET  /admin/inventory/{productId}    — 特定商品各倉庫存明細
///   POST /admin/stocks                   — 新增入庫（建 Stock + Warehousestocks）
/// </summary>
public sealed class InventoryAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public InventoryAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/warehouses
    public async Task<IActionResult> ListWarehouses(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT warehouseid, warehousetype, title FROM Warehouses ORDER BY warehousetype, title");
        return ctx.Ok(rows);
    }

    // POST /admin/warehouses
    public async Task<IActionResult> CreateWarehouse(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertWarehouseRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Warehouses (warehouseid, warehousetype, title) VALUES (@id, @warehousetype, @title)",
            new { id, warehousetype = body.WarehouseType, body.Title });

        return ctx.Created(new { warehouseId = id });
    }

    // PUT /admin/warehouses/{id}
    public async Task<IActionResult> UpdateWarehouse(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpsertWarehouseRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Warehouses SET warehousetype=@warehousetype, title=@title WHERE warehouseid=@id",
            new { id, warehousetype = body.WarehouseType, body.Title });

        if (rows == 0) return ctx.NotFound("找不到倉庫。");
        return ctx.Ok(new { message = "倉庫已更新" });
    }

    // DELETE /admin/warehouses/{id}
    public async Task<IActionResult> DeleteWarehouse(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var hasStock = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Warehousestocks WHERE warehouseid=@id AND quantity_left > 0", new { id });
        if (hasStock > 0) return ctx.UnprocessableEntity("此倉庫仍有庫存，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Warehouses WHERE warehouseid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到倉庫。");
        return ctx.Ok(new { message = "倉庫已刪除" });
    }

    // POST /admin/warehouse-transfer
    public async Task<IActionResult> TransferStock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<TransferStockRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.FromWarehouseId == Guid.Empty) return ctx.BadRequest("缺少 fromWarehouseId。");
        if (body.ToWarehouseId == Guid.Empty) return ctx.BadRequest("缺少 toWarehouseId。");
        if (body.FromWarehouseId == body.ToWarehouseId) return ctx.BadRequest("來源倉與目的倉不能相同。");
        if (body.ProductId == Guid.Empty) return ctx.BadRequest("缺少 productId。");
        if (body.Qty <= 0) return ctx.BadRequest("qty 必須大於 0。");

        var now = DateTime.UtcNow.AddHours(8);
        var today = DateOnly.FromDateTime(now);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        // FIFO: 取最近效期優先的 warehousestocks
        var sources = (await conn.QueryAsync<dynamic>(@"
SELECT ws.warehousestockid, ws.stockid, ws.quantity_left, s.expiredate
FROM Warehousestocks ws
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
WHERE ws.warehouseid=@fromId AND pd.productid=@productId AND ws.quantity_left > 0
ORDER BY s.expiredate ASC, ws.transdate ASC",
            new { fromId = body.FromWarehouseId, productId = body.ProductId })).ToList();

        var totalAvail = sources.Sum(s => (int)s.quantity_left);
        if (totalAvail < body.Qty)
            return ctx.UnprocessableEntity($"來源倉庫庫存不足（現有 {totalAvail}，需 {body.Qty}）。");

        int remaining = body.Qty;
        foreach (var src in sources)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, (int)src.quantity_left);

            await conn.ExecuteAsync(
                "UPDATE Warehousestocks SET quantity_left=quantity_left-@take WHERE warehousestockid=@id",
                new { take, id = (Guid)src.warehousestockid });

            await conn.ExecuteAsync(@"
INSERT INTO Warehousestocks (warehousestockid, stockid, warehouseid, quantity_left, transdate)
VALUES (NEWID(), @stockId, @toId, @qty, @today)",
                new { stockId = (Guid)src.stockid, toId = body.ToWarehouseId, qty = take, today });

            remaining -= take;
        }

        return ctx.Ok(new { message = $"已調撥 {body.Qty} 件至目的倉" });
    }

    // GET /admin/inventory?keyword=&warehouseId=&page=1&pageSize=20
    public async Task<IActionResult> ListInventory(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var keyword = q["keyword"].ToString().Trim();
        var warehouseId = Guid.TryParse(q["warehouseId"], out var wid) ? wid : (Guid?)null;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        // 各商品在各倉的現存量彙總
        var baseWhere = BuildInventoryWhere(keyword, warehouseId);
        var total = await conn.ExecuteScalarAsync<int>($@"
SELECT COUNT(DISTINCT pd.productid)
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products p ON p.productid=pd.productid
WHERE ws.quantity_left > 0 AND {baseWhere.Sql}", baseWhere.Params);

        var dp = new DynamicParameters(baseWhere.Params);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync<InventoryRow>($@"
SELECT pd.productid, p.title AS productTitle, p.productnum,
       w.warehouseid, w.title AS warehouseTitle,
       SUM(ws.quantity_left) AS totalQty,
       MIN(s.expiredate) AS nearestExpiry
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products p ON p.productid=pd.productid
WHERE ws.quantity_left > 0 AND {baseWhere.Sql}
GROUP BY pd.productid, p.title, p.productnum, w.warehouseid, w.title
ORDER BY p.title, w.title
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Select(r => (object)new
        {
            stockId = $"{r.productid}_{r.warehouseid}",
            productId = r.productid,
            r.productTitle,
            productNum = r.productnum,
            warehouseId = r.warehouseid,
            warehouseName = r.warehouseTitle,
            qty = r.totalQty,
            expireDate = r.nearestExpiry
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/inventory/{productId}
    public async Task<IActionResult> ProductInventory(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("productId"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var batches = await conn.QueryAsync<dynamic>(@"
SELECT ws.warehousestockid, ws.warehouseid, w.title AS warehouseTitle,
       s.stockid, s.expiredate, s.noticenumber AS noticenum,
       ws.quantity_left, ws.transdate
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
WHERE pd.productid = @productId AND ws.quantity_left > 0
ORDER BY s.expiredate ASC, ws.transdate ASC",
            new { productId });

        return ctx.Ok(batches);
    }

    // POST /admin/stocks — 人工入庫
    public async Task<IActionResult> AddStock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<AddStockRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.ProductId == Guid.Empty) return ctx.BadRequest("缺少 productId。");
        if (body.WarehouseId == Guid.Empty) return ctx.BadRequest("缺少 warehouseId。");
        if (body.Qty <= 0) return ctx.BadRequest("qty 必須大於 0。");
        if (body.PurchaseDetailId == Guid.Empty) return ctx.BadRequest("缺少 purchaseDetailId（需先建立採購單）。");

        var now = DateTime.UtcNow.AddHours(8);
        var today = DateOnly.FromDateTime(now);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        // 驗證倉庫與採購明細存在
        var warehouseExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Warehouses WHERE warehouseid=@id", new { id = body.WarehouseId });
        if (warehouseExists == 0) return ctx.NotFound("找不到倉庫。");

        var purchaseDetailExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Purchasedetails WHERE purchasedetailid=@id AND productid=@productId",
            new { id = body.PurchaseDetailId, productId = body.ProductId });
        if (purchaseDetailExists == 0)
            return ctx.NotFound("找不到採購明細，或採購明細的商品與 productId 不符。");

        var stockId = Guid.NewGuid();
        var warehouseStockId = Guid.NewGuid();

        await conn.ExecuteAsync(@"
INSERT INTO Stocks (stockid, purchasedetailid, stocktype, noticenum, declarationnum,
                   expiredate, qty, weight, createdate)
VALUES (@stockId, @purchaseDetailId, @stockType, @noticeNum, @declarationNum,
        @expireDate, @qty, @weight, @createdate);

INSERT INTO Warehousestocks (warehousestockid, stockid, warehouseid, quantity_left, transdate)
VALUES (@warehouseStockId, @stockId, @warehouseId, @qty, @transdate);",
            new
            {
                stockId,
                warehouseStockId,
                purchaseDetailId = body.PurchaseDetailId,
                stockType = body.StockType,
                noticeNum = body.NoticeNum,
                declarationNum = body.DeclarationNum,
                expireDate = body.ExpireDate,
                qty = body.Qty,
                weight = body.Weight,
                warehouseId = body.WarehouseId,
                createdate = now,
                transdate = today,
            });

        return ctx.Created(new { stockId, warehouseStockId });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static (string Sql, object Params) BuildInventoryWhere(string keyword, Guid? warehouseId)
    {
        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            clauses.Add("(p.title LIKE @kw OR p.productnum LIKE @kw)");
            p["kw"] = $"%{keyword}%";
        }
        if (warehouseId.HasValue) { clauses.Add("w.warehouseid = @wid"); p["wid"] = warehouseId.Value; }

        return (clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1", p);
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record InventoryRow(
        Guid productid, string productTitle, string? productnum,
        Guid warehouseid, string warehouseTitle,
        int totalQty, DateTime? nearestExpiry);

    private sealed record AddStockRequest(
        Guid ProductId, Guid WarehouseId, Guid PurchaseDetailId,
        int Qty, int StockType,
        string? NoticeNum, string? DeclarationNum,
        DateTime? ExpireDate, decimal? Weight);

    private sealed record UpsertWarehouseRequest(string Title, int WarehouseType);

    private sealed record TransferStockRequest(
        Guid ProductId, Guid FromWarehouseId, Guid ToWarehouseId, int Qty);
}
