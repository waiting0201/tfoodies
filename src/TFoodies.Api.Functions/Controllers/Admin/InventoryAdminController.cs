using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台庫存與倉庫管理（InventoryMs，對應舊系統 InventoryMsController）。三個子模組：
///
/// 倉儲維護（Warehouses）
///   GET    /admin/warehouses                         — 倉庫列表
///   POST   /admin/warehouses                         — 新增倉庫
///   PUT    /admin/warehouses/{id}                    — 更新倉庫
///   DELETE /admin/warehouses/{id}                    — 刪除倉庫（仍有庫存則不可刪）
///   GET    /admin/inventory                          — 各商品在各倉的現存量彙總（分頁）
///   GET    /admin/inventory/{productId}              — 特定商品各倉批次明細
///
/// 入庫維護（Stocks）
///   GET    /admin/stocks                             — 進貨批次列表（stocktype 分流、分頁）
///   GET    /admin/stocks/purchasable                 — 可入庫採購單下拉（status 1/3）
///   GET    /admin/stocks/purchasable/{id}/details    — 採購單明細下拉（連動）
///   GET    /admin/stocks/check-notice                — 通知號碼唯一性檢查
///   GET    /admin/stocks/{id}                        — 單一批次（供編輯）
///   POST   /admin/stocks                             — 新增入庫（建 Stock + Warehousestock + 推進採購狀態）
///   PUT    /admin/stocks/{id}                        — 編輯入庫（未出庫之單一批次同步數量）
///
/// 移庫維護（Warehousestocks）
///   GET    /admin/warehousestocks                    — 在庫/移倉帳列表（倉庫/關鍵字篩選、分頁）
///   GET    /admin/warehousestocks/source             — 指定倉可調撥批次（移倉連動）
///   PUT    /admin/warehousestocks/{id}               — 編輯在庫帳（數量/剩餘量/備註）
///   POST   /admin/warehousestocks/transfer           — 倉庫調撥（批次 FIFO 遞減來源、建目的批）
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

    // ── 倉儲維護 Warehouses ─────────────────────────────────────────────────────────

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

    // GET /admin/inventory?keyword=&warehouseId=&page=1&pageSize=20 — 各商品在各倉的現存量彙總
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

        var baseWhere = BuildInventoryWhere(keyword, warehouseId);
        var total = await conn.ExecuteScalarAsync<int>($@"
SELECT COUNT(*) FROM (
  SELECT 1 AS x
  FROM Warehousestocks ws
  JOIN Warehouses w ON w.warehouseid=ws.warehouseid
  JOIN Stocks s ON s.stockid=ws.stockid
  JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
  JOIN Products p ON p.productid=pd.productid
  WHERE ws.quantity_left > 0 AND {baseWhere.Sql}
  GROUP BY pd.productid, w.warehouseid
) t", baseWhere.Params);

        var dp = new DynamicParameters(baseWhere.Params);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = (await conn.QueryAsync<dynamic>($@"
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
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp))
            .Select(r => (object)new
            {
                productId = r.productid,
                productTitle = r.productTitle,
                productNum = r.productnum,
                warehouseId = r.warehouseid,
                warehouseName = r.warehouseTitle,
                qty = r.totalQty,
                expireDate = r.nearestExpiry,
            }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/inventory/{productId} — 特定商品各倉批次明細
    public async Task<IActionResult> ProductInventory(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("productId"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var batches = (await conn.QueryAsync<dynamic>(@"
SELECT ws.warehousestockid, ws.warehouseid, w.title AS warehouseTitle,
       s.stockid, s.expiredate, s.noticenumber,
       ws.quantity_left, ws.transdate
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
WHERE pd.productid = @productId AND ws.quantity_left > 0
ORDER BY s.expiredate ASC, ws.transdate ASC",
            new { productId }))
            .Select(b => (object)new
            {
                warehouseStockId = b.warehousestockid,
                warehouseId = b.warehouseid,
                warehouseName = b.warehouseTitle,
                stockId = b.stockid,
                noticeNumber = b.noticenumber,
                expireDate = b.expiredate,
                quantityLeft = b.quantity_left,
                transDate = b.transdate,
            }).ToList();

        return ctx.Ok(batches);
    }

    // ── 入庫維護 Stocks ─────────────────────────────────────────────────────────────

    // GET /admin/stocks?type=&keyword=&page=1&pageSize=20
    public async Task<IActionResult> ListStocks(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? type = int.TryParse(q["type"], out var t) ? t : null;
        var keyword = q["keyword"].ToString().Trim();
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();
        if (type is 1 or 2) { clauses.Add("s.stocktype = @type"); p["type"] = type.Value; }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            clauses.Add("(pr.title LIKE @kw OR pr.productnum LIKE @kw OR s.noticenumber LIKE @kw)");
            p["kw"] = $"%{keyword}%";
        }
        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>($@"
SELECT COUNT(*)
FROM Stocks s
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products pr ON pr.productid=pd.productid
WHERE {where}", p);

        var dp = new DynamicParameters(p);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = (await conn.QueryAsync<dynamic>($@"
SELECT s.stockid, s.stocktype, s.createdate, s.barcode, s.noticenumber, s.declarationnumber,
       s.item, s.manufacturedate, s.expiredate, s.quantity, s.weight, s.status,
       pr.productnum, pr.title AS productTitle,
       (SELECT ISNULL(SUM(ws.quantity_left),0) FROM Warehousestocks ws WHERE ws.stockid=s.stockid) AS remaining
FROM Stocks s
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products pr ON pr.productid=pd.productid
WHERE {where}
ORDER BY s.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp))
            .Select(r => (object)new
            {
                stockId = r.stockid,
                stockType = r.stocktype,
                createDate = r.createdate,
                barcode = r.barcode,
                noticeNumber = r.noticenumber,
                declarationNumber = r.declarationnumber,
                item = r.item,
                manufactureDate = r.manufacturedate,
                expireDate = r.expiredate,
                quantity = r.quantity,
                remaining = r.remaining,
                weight = r.weight,
                status = r.status,
                productNum = r.productnum,
                productTitle = r.productTitle,
            }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/stocks/purchasable — 可入庫採購單（status 1 未入庫 / 3 部分入庫）
    public async Task<IActionResult> PurchasableList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = (await conn.QueryAsync<dynamic>(@"
SELECT p.purchaseid, p.purchasecode, p.purchasedate, su.title AS supplierTitle
FROM Purchases p
JOIN Suppliers su ON su.supplierid=p.supplierid
WHERE p.status IN (1, 3)
ORDER BY p.purchasedate DESC, p.purchasecode DESC"))
            .Select(r => (object)new
            {
                purchaseId = r.purchaseid,
                purchaseCode = r.purchasecode,
                purchaseDate = r.purchasedate,
                supplierName = r.supplierTitle,
            }).ToList();

        return ctx.Ok(rows);
    }

    // GET /admin/stocks/purchasable/{id}/details — 採購單明細下拉（連動）
    public async Task<IActionResult> PurchaseDetailOptions(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var pid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = (await conn.QueryAsync<dynamic>(@"
SELECT pd.purchasedetailid, pd.productid, pd.qty, pd.status,
       pr.productnum, pr.title AS productTitle
FROM Purchasedetails pd
JOIN Products pr ON pr.productid=pd.productid
WHERE pd.purchaseid=@pid
ORDER BY pr.title",
            new { pid }))
            .Select(r => (object)new
            {
                purchaseDetailId = r.purchasedetailid,
                productId = r.productid,
                productNum = r.productnum,
                productTitle = r.productTitle,
                qty = r.qty,
                status = r.status,
            }).ToList();

        return ctx.Ok(rows);
    }

    // GET /admin/stocks/check-notice?noticeNumber=&excludeId=
    public async Task<IActionResult> CheckNotice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var notice = ctx.Request.Query["noticeNumber"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(notice)) return ctx.Ok(new { valid = true });
        Guid? excludeId = Guid.TryParse(ctx.Request.Query["excludeId"], out var ex) ? ex : null;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var cnt = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Stocks WHERE noticenumber=@notice AND (@ex IS NULL OR stockid<>@ex)",
            new { notice, ex = excludeId });

        return ctx.Ok(new { valid = cnt == 0 });
    }

    // GET /admin/stocks/{id} — 單一批次（供編輯）
    public async Task<IActionResult> StockDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var r = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT s.stockid, s.purchaseid, s.purchasedetailid, s.stocktype, s.createdate,
       s.barcode, s.noticenumber, s.declarationnumber, s.item,
       s.manufacturedate, s.expiredate, s.quantity, s.weight, s.status,
       p.purchasecode, pd.productid, pd.status AS detailStatus,
       pr.productnum, pr.title AS productTitle,
       (SELECT COUNT(1) FROM Warehousestocks ws WHERE ws.stockid=s.stockid) AS warehouseStockCount,
       (SELECT ISNULL(SUM(ws.quantity_left),0) FROM Warehousestocks ws WHERE ws.stockid=s.stockid) AS remaining
FROM Stocks s
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Purchases p ON p.purchaseid=s.purchaseid
JOIN Products pr ON pr.productid=pd.productid
WHERE s.stockid=@id",
            new { id });

        if (r is null) return ctx.NotFound("找不到入庫批次。");

        return ctx.Ok(new
        {
            stockId = r.stockid,
            purchaseId = r.purchaseid,
            purchaseCode = r.purchasecode,
            purchaseDetailId = r.purchasedetailid,
            productId = r.productid,
            productNum = r.productnum,
            productTitle = r.productTitle,
            stockType = r.stocktype,
            detailStatus = r.detailStatus,
            createDate = r.createdate,
            barcode = r.barcode,
            noticeNumber = r.noticenumber,
            declarationNumber = r.declarationnumber,
            item = r.item,
            manufactureDate = r.manufacturedate,
            expireDate = r.expiredate,
            quantity = r.quantity,
            remaining = r.remaining,
            weight = r.weight,
            status = r.status,
            // 已有出庫（剩餘量 != 入庫量）或拆成多筆在庫帳時，數量不可改
            quantityEditable = r.warehouseStockCount == 1 && r.remaining == r.quantity,
        });
    }

    // POST /admin/stocks — 新增入庫（建 Stock + Warehousestock + 推進採購狀態）
    public async Task<IActionResult> AddStock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<AddStockRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.PurchaseDetailId == Guid.Empty) return ctx.BadRequest("缺少 purchaseDetailId（需先建立採購單）。");
        if (body.WarehouseId == Guid.Empty) return ctx.BadRequest("缺少 warehouseId。");
        if (body.Quantity <= 0) return ctx.BadRequest("數量必須大於 0。");
        if (body.StockType is not (1 or 2)) return ctx.BadRequest("stockType 必須為 1（需申報）或 2（不需申報）。");
        if (body.DetailStatus is not (1 or 2 or 3)) return ctx.BadRequest("detailStatus 必須為 1（完整）/2（有缺）/3（有多）。");

        var now = DateTime.UtcNow.AddHours(8);
        var createDate = body.CreateDate ?? DateOnly.FromDateTime(now);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        // 採購明細存在性 + 取得 purchaseid（以 DB 為準，不信任前端帶入）
        var pd = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT purchaseid FROM Purchasedetails WHERE purchasedetailid=@id",
            new { id = body.PurchaseDetailId });
        if (pd is null) return ctx.NotFound("找不到採購明細。");
        Guid purchaseId = pd.purchaseid;

        var warehouseExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Warehouses WHERE warehouseid=@id", new { id = body.WarehouseId });
        if (warehouseExists == 0) return ctx.NotFound("找不到倉庫。");

        if (body.StockType == 1 && !string.IsNullOrWhiteSpace(body.NoticeNumber))
        {
            var dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Stocks WHERE noticenumber=@n", new { n = body.NoticeNumber });
            if (dup > 0) return ctx.UnprocessableEntity("通知號碼已存在。");
        }

        var stockId = Guid.NewGuid();
        var warehouseStockId = Guid.NewGuid();

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync(@"
INSERT INTO Stocks (stockid, purchasedetailid, purchaseid, stocktype, createdate, barcode,
    noticenumber, declarationnumber, item, manufacturedate, expiredate, quantity, weight, status)
VALUES (@stockId, @purchaseDetailId, @purchaseId, @stockType, @createDate, @barcode,
    @noticeNumber, @declarationNumber, @item, @manufactureDate, @expireDate, @quantity, @weight, @status)",
                new
                {
                    stockId,
                    purchaseDetailId = body.PurchaseDetailId,
                    purchaseId,
                    stockType = body.StockType,
                    createDate,
                    barcode = body.StockType == 1 ? body.Barcode : null,
                    noticeNumber = body.StockType == 1 ? body.NoticeNumber : null,
                    declarationNumber = body.StockType == 1 ? body.DeclarationNumber : null,
                    item = body.StockType == 1 ? body.Item : null,
                    manufactureDate = body.StockType == 1 ? body.ManufactureDate : null,
                    expireDate = body.StockType == 1 ? body.ExpireDate : null,
                    quantity = body.Quantity,
                    weight = body.StockType == 1 ? body.Weight : null,
                    status = body.StockType == 1 ? body.Status : null,
                }, tx);

            await conn.ExecuteAsync(@"
INSERT INTO Warehousestocks (warehousestockid, warehouseid, stockid, quantity, quantity_left,
    transdate, memo, createdate)
VALUES (@warehouseStockId, @warehouseId, @stockId, @quantity, @quantity, @transdate, NULL, @now)",
                new { warehouseStockId, warehouseId = body.WarehouseId, stockId, quantity = body.Quantity, transdate = now, now }, tx);

            await CheckPurchaseStatusAsync(conn, tx, body.PurchaseDetailId, body.DetailStatus);

            tx.Commit();
            return ctx.Created(new { stockId, warehouseStockId });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/stocks/{id} — 編輯入庫
    public async Task<IActionResult> UpdateStock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateStockRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.Quantity <= 0) return ctx.BadRequest("數量必須大於 0。");
        if (body.DetailStatus is not (1 or 2 or 3)) return ctx.BadRequest("detailStatus 必須為 1/2/3。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var stock = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT stocktype, purchasedetailid FROM Stocks WHERE stockid=@id", new { id });
        if (stock is null) return ctx.NotFound("找不到入庫批次。");
        int stockType = stock.stocktype;
        Guid purchaseDetailId = stock.purchasedetailid;

        // 通知號碼唯一性（排除自己）
        if (stockType == 1 && !string.IsNullOrWhiteSpace(body.NoticeNumber))
        {
            var dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Stocks WHERE noticenumber=@n AND stockid<>@id",
                new { n = body.NoticeNumber, id });
            if (dup > 0) return ctx.UnprocessableEntity("通知號碼已存在。");
        }

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync(@"
UPDATE Stocks SET
    barcode=@barcode, noticenumber=@noticeNumber, declarationnumber=@declarationNumber, item=@item,
    manufacturedate=@manufactureDate, expiredate=@expireDate, quantity=@quantity, weight=@weight,
    status=@status, createdate=ISNULL(@createDate, createdate)
WHERE stockid=@id",
                new
                {
                    id,
                    barcode = stockType == 1 ? body.Barcode : null,
                    noticeNumber = stockType == 1 ? body.NoticeNumber : null,
                    declarationNumber = stockType == 1 ? body.DeclarationNumber : null,
                    item = stockType == 1 ? body.Item : null,
                    manufactureDate = stockType == 1 ? body.ManufactureDate : null,
                    expireDate = stockType == 1 ? body.ExpireDate : null,
                    quantity = body.Quantity,
                    weight = stockType == 1 ? body.Weight : null,
                    status = stockType == 1 ? body.Status : null,
                    createDate = body.CreateDate,
                }, tx);

            // 僅單一且未出庫（quantity == quantity_left）之在庫帳同步數量
            var wsRows = (await conn.QueryAsync<dynamic>(
                "SELECT warehousestockid, quantity, quantity_left FROM Warehousestocks WHERE stockid=@id",
                new { id }, tx)).ToList();
            if (wsRows.Count == 1 && (int)wsRows[0].quantity == (int)wsRows[0].quantity_left)
            {
                await conn.ExecuteAsync(
                    "UPDATE Warehousestocks SET quantity=@q, quantity_left=@q WHERE warehousestockid=@wsid",
                    new { q = body.Quantity, wsid = (Guid)wsRows[0].warehousestockid }, tx);
            }

            await CheckPurchaseStatusAsync(conn, tx, purchaseDetailId, body.DetailStatus);

            tx.Commit();
            return ctx.Ok(new { message = "入庫批次已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 移庫維護 Warehousestocks ────────────────────────────────────────────────────

    // GET /admin/warehousestocks?warehouseId=&keyword=&page=1&pageSize=20
    public async Task<IActionResult> ListWarehousestocks(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var warehouseId = Guid.TryParse(q["warehouseId"], out var wid) ? wid : (Guid?)null;
        var keyword = q["keyword"].ToString().Trim();
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();
        if (warehouseId.HasValue) { clauses.Add("ws.warehouseid = @wid"); p["wid"] = warehouseId.Value; }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            clauses.Add("(pr.title LIKE @kw OR pr.productnum LIKE @kw OR s.noticenumber LIKE @kw)");
            p["kw"] = $"%{keyword}%";
        }
        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>($@"
SELECT COUNT(*)
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products pr ON pr.productid=pd.productid
WHERE {where}", p);

        var dp = new DynamicParameters(p);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = (await conn.QueryAsync<dynamic>($@"
SELECT ws.warehousestockid, ws.transdate, ws.quantity, ws.quantity_left, ws.memo,
       w.warehouseid, w.title AS warehouseTitle,
       s.stockid, s.noticenumber, s.expiredate,
       pr.productnum, pr.title AS productTitle
FROM Warehousestocks ws
JOIN Warehouses w ON w.warehouseid=ws.warehouseid
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products pr ON pr.productid=pd.productid
WHERE {where}
ORDER BY ws.transdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp))
            .Select(r => (object)new
            {
                warehouseStockId = r.warehousestockid,
                transDate = r.transdate,
                quantity = r.quantity,
                quantityLeft = r.quantity_left,
                memo = r.memo,
                warehouseId = r.warehouseid,
                warehouseName = r.warehouseTitle,
                stockId = r.stockid,
                noticeNumber = r.noticenumber,
                expireDate = r.expiredate,
                productNum = r.productnum,
                productTitle = r.productTitle,
            }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/warehousestocks/source?warehouseId= — 指定倉可調撥批次（移倉連動）
    public async Task<IActionResult> SourceStocks(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.Request.Query["warehouseId"], out var wid))
            return ctx.BadRequest("缺少 warehouseId。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = (await conn.QueryAsync<dynamic>(@"
SELECT s.stockid, s.noticenumber, s.expiredate,
       pr.productnum, pr.title AS productTitle,
       SUM(ws.quantity_left) AS available
FROM Warehousestocks ws
JOIN Stocks s ON s.stockid=ws.stockid
JOIN Purchasedetails pd ON pd.purchasedetailid=s.purchasedetailid
JOIN Products pr ON pr.productid=pd.productid
WHERE ws.warehouseid=@wid AND ws.quantity_left > 0
GROUP BY s.stockid, s.noticenumber, s.expiredate, pr.productnum, pr.title
HAVING SUM(ws.quantity_left) > 0
ORDER BY pr.title, s.expiredate",
            new { wid }))
            .Select(r => (object)new
            {
                stockId = r.stockid,
                noticeNumber = r.noticenumber,
                expireDate = r.expiredate,
                productNum = r.productnum,
                productTitle = r.productTitle,
                available = r.available,
            }).ToList();

        return ctx.Ok(rows);
    }

    // PUT /admin/warehousestocks/{id} — 編輯在庫帳（數量/剩餘量/備註）
    public async Task<IActionResult> UpdateWarehousestock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateWarehousestockRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.Quantity < 0 || body.QuantityLeft < 0) return ctx.BadRequest("數量不可為負。");
        if (body.QuantityLeft > body.Quantity) return ctx.BadRequest("剩餘量不可大於入庫量。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Warehousestocks SET quantity=@q, quantity_left=@ql, memo=@memo WHERE warehousestockid=@id",
            new { id, q = body.Quantity, ql = body.QuantityLeft, memo = body.Memo });

        if (rows == 0) return ctx.NotFound("找不到在庫帳。");
        return ctx.Ok(new { message = "在庫帳已更新" });
    }

    // POST /admin/warehousestocks/transfer — 倉庫調撥（批次 FIFO 遞減來源、建目的批）
    public async Task<IActionResult> TransferStock(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InventoryMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<TransferRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (body.StockId == Guid.Empty) return ctx.BadRequest("缺少 stockId。");
        if (body.FromWarehouseId == Guid.Empty) return ctx.BadRequest("缺少 fromWarehouseId。");
        if (body.ToWarehouseId == Guid.Empty) return ctx.BadRequest("缺少 toWarehouseId。");
        if (body.FromWarehouseId == body.ToWarehouseId) return ctx.BadRequest("來源倉與目的倉不能相同。");
        if (body.Qty <= 0) return ctx.BadRequest("數量必須大於 0。");

        var now = DateTime.UtcNow.AddHours(8);
        var transDate = body.TransDate?.ToDateTime(TimeOnly.MinValue) ?? now;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 來源倉該批次各筆在庫帳（FIFO：transdate 先進先出）
            var sources = (await conn.QueryAsync<dynamic>(@"
SELECT warehousestockid, quantity_left
FROM Warehousestocks WITH (UPDLOCK, ROWLOCK)
WHERE warehouseid=@from AND stockid=@stock AND quantity_left > 0
ORDER BY transdate ASC",
                new { from = body.FromWarehouseId, stock = body.StockId }, tx)).ToList();

            var totalAvail = sources.Sum(s => (int)s.quantity_left);
            if (totalAvail < body.Qty)
            {
                tx.Rollback();
                return ctx.UnprocessableEntity($"來源倉庫存不足（現有 {totalAvail}，需 {body.Qty}）。");
            }

            int remaining = body.Qty;
            foreach (var src in sources)
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, (int)src.quantity_left);
                await conn.ExecuteAsync(
                    "UPDATE Warehousestocks SET quantity_left=quantity_left-@take WHERE warehousestockid=@id",
                    new { take, id = (Guid)src.warehousestockid }, tx);
                remaining -= take;
            }

            await conn.ExecuteAsync(@"
INSERT INTO Warehousestocks (warehousestockid, warehouseid, stockid, quantity, quantity_left,
    transdate, memo, createdate)
VALUES (NEWID(), @to, @stock, @qty, @qty, @transDate, @memo, @now)",
                new { to = body.ToWarehouseId, stock = body.StockId, qty = body.Qty, transDate, memo = body.Memo, now }, tx);

            tx.Commit();
            return ctx.Ok(new { message = $"已調撥 {body.Qty} 件至目的倉" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 推進採購狀態（移植舊系統 General.CheckPurchaseStatus）：
    /// 設定該明細的入庫狀態，再依採購單下全部明細重算 Purchases.status
    /// （全未入庫=1、全已入庫=2、部分入庫=3）。
    /// </summary>
    private static Task CheckPurchaseStatusAsync(
        SqlConnection conn, SqlTransaction tx, Guid purchaseDetailId, int detailStatus)
        => conn.ExecuteAsync(@"
UPDATE Purchasedetails SET status=@ds WHERE purchasedetailid=@pdid;
DECLARE @pid uniqueidentifier = (SELECT purchaseid FROM Purchasedetails WHERE purchasedetailid=@pdid);
DECLARE @total int = (SELECT COUNT(*) FROM Purchasedetails WHERE purchaseid=@pid);
DECLARE @notin int = (SELECT COUNT(*) FROM Purchasedetails WHERE purchaseid=@pid AND status=0);
UPDATE Purchases
SET status = CASE WHEN @notin=@total THEN 1 WHEN @notin=0 THEN 2 ELSE 3 END
WHERE purchaseid=@pid;",
            new { ds = detailStatus, pdid = purchaseDetailId }, tx);

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

    // ── Request DTO types ─────────────────────────────────────────────────────────

    private sealed record UpsertWarehouseRequest(string Title, int WarehouseType);

    private sealed record AddStockRequest(
        Guid PurchaseDetailId, Guid WarehouseId, int StockType, int DetailStatus,
        DateOnly? CreateDate, string? Barcode, string? NoticeNumber, string? DeclarationNumber,
        int? Item, DateOnly? ManufactureDate, DateOnly? ExpireDate,
        int Quantity, decimal? Weight, int? Status);

    private sealed record UpdateStockRequest(
        int DetailStatus, DateOnly? CreateDate, string? Barcode, string? NoticeNumber,
        string? DeclarationNumber, int? Item, DateOnly? ManufactureDate, DateOnly? ExpireDate,
        int Quantity, decimal? Weight, int? Status);

    private sealed record TransferRequest(
        Guid StockId, Guid FromWarehouseId, Guid ToWarehouseId, int Qty, DateOnly? TransDate, string? Memo);

    private sealed record UpdateWarehousestockRequest(int Quantity, int QuantityLeft, string? Memo);
}
