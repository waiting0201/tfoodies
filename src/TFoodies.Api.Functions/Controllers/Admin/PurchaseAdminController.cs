using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台採購與供應商管理（PurchaseMs，對應舊系統 PurchaseMsController）。
///   GET    /admin/suppliers                  — 供應商列表
///   POST   /admin/suppliers                  — 新增供應商
///   PUT    /admin/suppliers/{id}             — 更新供應商
///   DELETE /admin/suppliers/{id}             — 刪除供應商（仍有採購單則不可刪）
///   GET    /admin/exchanges                  — 幣別/匯率清單（採購表單下拉用）
///   GET    /admin/purchases                  — 採購單列表（狀態/供應商篩選、分頁）
///   GET    /admin/purchases/{id}             — 採購單明細（含 Purchasedetails）
///   POST   /admin/purchases                  — 新增採購單（產生 purchasecode、status=1、含明細）
///   PUT    /admin/purchases/{id}             — 更新採購單（含明細差異比對；已轉應付不可編輯）
///   PATCH  /admin/purchases/{id}/expenditure — 轉應付憑單
/// </summary>
public sealed class PurchaseAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;

    public PurchaseAdminController(
        IAdminPermissionService perms, IDbConnectionFactory db, ICodeNumberService codes)
    {
        _perms = perms; _db = db; _codes = codes;
    }

    // ── Suppliers ─────────────────────────────────────────────────────────────────

    // GET /admin/suppliers
    public async Task<IActionResult> ListSuppliers(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT supplierid, title, contactor, phone, address FROM Suppliers ORDER BY title");

        var items = rows.Select(r => (object)new
        {
            supplierId = r.supplierid,
            r.title,
            r.contactor,
            r.phone,
            r.address,
        }).ToList();
        return ctx.Ok(items);
    }

    // POST /admin/suppliers
    public async Task<IActionResult> CreateSupplier(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<SupplierRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Suppliers (supplierid, title, contactor, phone, address) VALUES (@id, @title, @contactor, @phone, @address)",
            new
            {
                id,
                title = body.Title.Trim(),
                contactor = body.Contactor ?? string.Empty,
                phone = body.Phone ?? string.Empty,
                address = body.Address ?? string.Empty,
            });
        return ctx.Created(new { supplierId = id });
    }

    // PUT /admin/suppliers/{id}
    public async Task<IActionResult> UpdateSupplier(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var sid)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<SupplierRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Suppliers SET title=@title, contactor=@contactor, phone=@phone, address=@address WHERE supplierid=@sid",
            new
            {
                sid,
                title = body.Title.Trim(),
                contactor = body.Contactor ?? string.Empty,
                phone = body.Phone ?? string.Empty,
                address = body.Address ?? string.Empty,
            });
        if (rows == 0) return ctx.NotFound("找不到供應商");
        return ctx.Ok(new { message = "供應商已更新" });
    }

    // DELETE /admin/suppliers/{id}
    public async Task<IActionResult> DeleteSupplier(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var sid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var inUse = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Purchases WHERE supplierid=@sid", new { sid });
        if (inUse > 0) return ctx.UnprocessableEntity("此供應商仍有採購單，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Suppliers WHERE supplierid=@sid", new { sid });
        if (rows == 0) return ctx.NotFound("找不到供應商");
        return ctx.Ok(new { message = "供應商已刪除" });
    }

    // ── Exchanges（幣別／匯率，採購表單下拉用；維護歸 AccountingMs） ─────────────────

    // GET /admin/exchanges
    public async Task<IActionResult> ListExchanges(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT exchangeid, title, rate FROM Exchanges ORDER BY title");

        var items = rows.Select(r => (object)new
        {
            exchangeId = r.exchangeid,
            r.title,
            r.rate,
        }).ToList();
        return ctx.Ok(items);
    }

    // ── Purchases ─────────────────────────────────────────────────────────────────

    // GET /admin/purchases?status=&supplierId=&page=1&pageSize=20
    public async Task<IActionResult> ListPurchases(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? status = int.TryParse(q["status"], out var s) ? s : null;
        var supplierId = Guid.TryParse(q["supplierId"], out var sid) ? sid : (Guid?)null;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();
        if (status.HasValue) { clauses.Add("p.status = @status"); p["status"] = status.Value; }
        if (supplierId.HasValue) { clauses.Add("p.supplierid = @sid"); p["sid"] = supplierId.Value; }
        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Purchases p WHERE {where}", p);

        var dp = new DynamicParameters(p);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var rows = await conn.QueryAsync($@"
SELECT p.purchaseid, p.purchasecode, p.purchasedate, p.status, p.isexpenditure,
       p.supplierid, s.title AS supplierTitle, p.exchangeid, e.title AS exchangeTitle,
       p.payment, p.etd, p.deliverterm, p.createdate,
       (SELECT ISNULL(SUM(pd.subtotal),0) FROM Purchasedetails pd WHERE pd.purchaseid=p.purchaseid) AS total
FROM Purchases p
JOIN Suppliers s ON s.supplierid=p.supplierid
LEFT JOIN Exchanges e ON e.exchangeid=p.exchangeid
WHERE {where}
ORDER BY p.purchasedate DESC, p.purchasecode DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            purchaseId = r.purchaseid,
            purchaseCode = r.purchasecode,
            purchaseDate = r.purchasedate,
            status = r.status,
            isExpenditure = r.isexpenditure,
            supplierId = r.supplierid,
            supplierName = r.supplierTitle,
            exchangeId = r.exchangeid,
            exchangeName = r.exchangeTitle,
            payment = r.payment,
            etd = r.etd,
            deliverTerm = r.deliverterm,
            total = r.total,
            createdAt = r.createdate,
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/purchases/export?purchaseIds=guid,guid（勾選匯出）或 ?status=&supplierId=（依篩選匯出）
    // 對應舊系統 PurchaseMs 的「匯出」按鈕（舊系統 PurchasesExport 未實作；此處比照訂單匯出以 ClosedXML 產生）。
    public async Task<IActionResult> ExportPurchases(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var ids = q["purchaseIds"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue).Select(g => g!.Value).ToList();
        int? status = int.TryParse(q["status"], out var s) ? s : null;
        var supplierId = Guid.TryParse(q["supplierId"], out var sid) ? sid : (Guid?)null;

        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();
        if (ids.Count > 0) { clauses.Add("p.purchaseid IN @ids"); p["ids"] = ids; }
        else
        {
            if (status.HasValue) { clauses.Add("p.status = @status"); p["status"] = status.Value; }
            if (supplierId.HasValue) { clauses.Add("p.supplierid = @sid"); p["sid"] = supplierId.Value; }
        }
        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var headers = (await conn.QueryAsync<PurchaseExportHeaderRow>($@"
SELECT p.purchaseid, p.purchasecode, p.purchasedate, p.status,
       s.title AS supplierTitle, e.title AS exchangeTitle,
       p.payment, p.etd, p.deliverterm, p.note
FROM Purchases p
JOIN Suppliers s ON s.supplierid=p.supplierid
LEFT JOIN Exchanges e ON e.exchangeid=p.exchangeid
WHERE {where}
ORDER BY p.purchasedate DESC, p.purchasecode DESC", p)).ToList();

        var purchaseIds = headers.Select(h => h.purchaseid).ToList();
        var lines = purchaseIds.Count == 0
            ? new List<PurchaseExportLineRow>()
            : (await conn.QueryAsync<PurchaseExportLineRow>(@"
SELECT pd.purchaseid, pr.productnum, pr.title AS productTitle, pd.unitprice, pd.qty, pd.subtotal
FROM Purchasedetails pd
JOIN Products pr ON pr.productid=pd.productid
WHERE pd.purchaseid IN @ids
ORDER BY pr.productnum", new { ids = purchaseIds })).ToList();

        var models = headers.Select(h => new PurchaseExcelReport.PurchaseExportModel(
            h.purchasecode, h.purchasedate, h.supplierTitle, h.exchangeTitle,
            h.payment, h.etd, h.deliverterm, h.status, h.note,
            lines.Where(l => l.purchaseid == h.purchaseid)
                 .Select(l => new PurchaseExcelReport.PurchaseExportLine(
                     l.productnum, l.productTitle, l.unitprice, l.qty, l.subtotal)).ToList()
        )).ToList();

        var bytes = PurchaseExcelReport.BuildPurchasesSheet(models);
        var fileName = $"{DateTime.UtcNow.AddHours(8):yyyyMMdd}_purchases_export.xlsx";
        return ctx.File(bytes, PurchaseExcelReport.ContentType, fileName);
    }

    // GET /admin/purchases/{id}
    public async Task<IActionResult> DetailPurchase(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var pid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var multi = await conn.QueryMultipleAsync(@"
SELECT p.purchaseid, p.purchasecode, p.purchasedate, p.status, p.isexpenditure,
       p.supplierid, s.title AS supplierTitle, p.exchangeid, e.title AS exchangeTitle,
       p.payment, p.etd, p.deliverterm, p.note, p.createdate
FROM Purchases p
JOIN Suppliers s ON s.supplierid=p.supplierid
LEFT JOIN Exchanges e ON e.exchangeid=p.exchangeid
WHERE p.purchaseid=@pid;
SELECT pd.purchasedetailid, pd.productid, pr.title AS productTitle, pr.productnum AS productNum,
       pd.unitprice, pd.qty, pd.subtotal, pd.status
FROM Purchasedetails pd
JOIN Products pr ON pr.productid=pd.productid
WHERE pd.purchaseid=@pid;",
            new { pid });

        var h = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (h is null) return ctx.NotFound("找不到採購單");

        var purchase = new
        {
            purchaseId = h.purchaseid,
            purchaseCode = h.purchasecode,
            purchaseDate = h.purchasedate,
            status = h.status,
            isExpenditure = h.isexpenditure,
            supplierId = h.supplierid,
            supplierName = h.supplierTitle,
            exchangeId = h.exchangeid,
            exchangeName = h.exchangeTitle,
            payment = h.payment,
            etd = h.etd,
            deliverTerm = h.deliverterm,
            note = h.note,
            createdAt = h.createdate,
        };

        var details = (await multi.ReadAsync<dynamic>()).Select(d => (object)new
        {
            purchaseDetailId = d.purchasedetailid,
            productId = d.productid,
            productTitle = d.productTitle,
            productNum = d.productNum,
            unitPrice = d.unitprice,
            qty = d.qty,
            subtotal = d.subtotal,
            status = d.status,
        }).ToList();

        return ctx.Ok(new { purchase, details });
    }

    // POST /admin/purchases
    public async Task<IActionResult> CreatePurchase(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertPurchaseRequest>();
        if (body is null || body.SupplierId == Guid.Empty) return ctx.BadRequest("缺少 supplierId。");
        if (body.ExchangeId == Guid.Empty) return ctx.BadRequest("缺少 exchangeId（幣別）。");
        if (body.Items is null || body.Items.Count == 0) return ctx.BadRequest("明細不能為空。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var purchaseCode = await _codes.NextAsync(CodeKind.Purchase, today, tx);
            var purchaseId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Purchases (purchaseid, supplierid, purchasecode, purchasedate, exchangeid, etd,
    payment, deliverterm, note, createdate, status, isexpenditure)
VALUES (@purchaseid, @supplierid, @purchasecode, @purchasedate, @exchangeid, @etd,
    @payment, @deliverterm, @note, @createdate, 1, 0)",
                new
                {
                    purchaseid = purchaseId,
                    supplierid = body.SupplierId,
                    purchasecode = purchaseCode,
                    purchasedate = body.PurchaseDate ?? today,
                    exchangeid = body.ExchangeId,
                    etd = body.Etd,
                    payment = body.Payment ?? string.Empty,
                    deliverterm = body.DeliverTerm,
                    note = body.Note,
                    createdate = today,
                }, tx);

            foreach (var line in body.Items)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Purchasedetails (purchasedetailid, purchaseid, productid, unitprice, qty, subtotal, status)
VALUES (NEWID(), @purchaseid, @productid, @unitprice, @qty, @subtotal, 0)",
                    new
                    {
                        purchaseid = purchaseId,
                        productid = line.ProductId,
                        unitprice = line.UnitPrice,
                        qty = line.Qty,
                        subtotal = line.UnitPrice * line.Qty,
                    }, tx);
            }

            tx.Commit();
            return ctx.Created(new { purchaseId, purchaseCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/purchases/{id}
    public async Task<IActionResult> UpdatePurchase(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var pid)) return ctx.BadRequest("無效的 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertPurchaseRequest>();
        if (body is null || body.SupplierId == Guid.Empty) return ctx.BadRequest("缺少 supplierId。");
        if (body.ExchangeId == Guid.Empty) return ctx.BadRequest("缺少 exchangeId（幣別）。");
        if (body.Items is null || body.Items.Count == 0) return ctx.BadRequest("明細不能為空。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var existing = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT purchaseid, isexpenditure FROM Purchases WHERE purchaseid=@pid", new { pid }, tx);
            if (existing is null) return ctx.NotFound("找不到採購單");
            if ((bool)existing.isexpenditure) return ctx.UnprocessableEntity("已轉應付憑單的採購單不可再編輯。");

            await conn.ExecuteAsync(@"
UPDATE Purchases SET supplierid=@supplierid, purchasedate=@purchasedate, exchangeid=@exchangeid,
    etd=@etd, payment=@payment, deliverterm=@deliverterm, note=@note
WHERE purchaseid=@pid",
                new
                {
                    pid,
                    supplierid = body.SupplierId,
                    purchasedate = body.PurchaseDate,
                    exchangeid = body.ExchangeId,
                    etd = body.Etd,
                    payment = body.Payment ?? string.Empty,
                    deliverterm = body.DeliverTerm,
                    note = body.Note,
                }, tx);

            // 取現有明細 ID
            var existingDetailIds = (await conn.QueryAsync<Guid>(
                "SELECT purchasedetailid FROM Purchasedetails WHERE purchaseid=@pid", new { pid }, tx)).ToHashSet();

            var submittedIds = body.Items
                .Where(l => l.PurchaseDetailId.HasValue)
                .Select(l => l.PurchaseDetailId!.Value)
                .ToHashSet();

            // DELETE 已移除的明細
            foreach (var id in existingDetailIds.Except(submittedIds))
                await conn.ExecuteAsync(
                    "DELETE FROM Purchasedetails WHERE purchasedetailid=@id", new { id }, tx);

            // UPDATE 或 INSERT 其餘明細
            foreach (var line in body.Items)
            {
                if (line.PurchaseDetailId.HasValue && existingDetailIds.Contains(line.PurchaseDetailId.Value))
                {
                    await conn.ExecuteAsync(@"
UPDATE Purchasedetails SET productid=@productid, unitprice=@unitprice, qty=@qty, subtotal=@subtotal
WHERE purchasedetailid=@id",
                        new { id = line.PurchaseDetailId.Value, productid = line.ProductId, unitprice = line.UnitPrice, qty = line.Qty, subtotal = line.UnitPrice * line.Qty }, tx);
                }
                else
                {
                    await conn.ExecuteAsync(@"
INSERT INTO Purchasedetails (purchasedetailid, purchaseid, productid, unitprice, qty, subtotal, status)
VALUES (NEWID(), @purchaseid, @productid, @unitprice, @qty, @subtotal, 0)",
                        new { purchaseid = pid, productid = line.ProductId, unitprice = line.UnitPrice, qty = line.Qty, subtotal = line.UnitPrice * line.Qty }, tx);
                }
            }

            tx.Commit();
            return ctx.Ok(new { message = "採購單已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // PATCH /admin/purchases/{id}/expenditure — 轉應付憑單
    public async Task<IActionResult> ToExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "PurchaseMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var pid)) return ctx.BadRequest("無效的 ID。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var purchase = await conn.QuerySingleOrDefaultAsync<PurchaseRow>(
                "SELECT purchaseid, supplierid, isexpenditure FROM Purchases WHERE purchaseid=@pid", new { pid }, tx);
            if (purchase is null) return ctx.NotFound("找不到採購單");
            if (purchase.isexpenditure) return ctx.UnprocessableEntity("此採購單已轉應付憑單。");

            var expCode = await _codes.NextAsync(CodeKind.Expenditure, today, tx);
            var expId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Expenditures (expenditureid, supplierid, expenditurecode, expendituredate,
    sourcetype, purchaseid, status, createdate)
VALUES (@expId, @supplierid, @expCode, @today, 1, @pid, 0, @now)",
                new { expId, supplierid = purchase.supplierid, expCode, today, pid, now }, tx);

            // 以採購明細建立 Expendituredetails（需要 accountingid，暫置 null）
            var details = await conn.QueryAsync<PurchaseDetailRow>(
                "SELECT purchasedetailid, productid, subtotal FROM Purchasedetails WHERE purchaseid=@pid",
                new { pid }, tx);

            foreach (var d in details)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Expendituredetails (expendituredetailid, expenditureid, accountingid, price, summary)
VALUES (NEWID(), @expId, NULL, @price, @summary)",
                    new { expId, price = (int)d.subtotal, summary = d.productid.ToString() }, tx);
            }

            await conn.ExecuteAsync(
                "UPDATE Purchases SET isexpenditure=1 WHERE purchaseid=@pid", new { pid }, tx);

            tx.Commit();
            return ctx.Created(new { expenditureId = expId, expenditureCode = expCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record SupplierRequest(string Title, string? Contactor, string? Phone, string? Address);
    private sealed record UpsertPurchaseRequest(
        Guid SupplierId, Guid ExchangeId,
        DateOnly? PurchaseDate, DateOnly? Etd,
        string? Payment, string? DeliverTerm, string? Note,
        List<PurchaseLineRequest>? Items);
    private sealed record PurchaseLineRequest(Guid ProductId, decimal UnitPrice, int Qty, Guid? PurchaseDetailId = null);
    private sealed record PurchaseRow(Guid purchaseid, Guid supplierid, bool isexpenditure);
    private sealed record PurchaseDetailRow(Guid purchasedetailid, Guid productid, decimal subtotal);
    private sealed record PurchaseExportHeaderRow(
        Guid purchaseid, string purchasecode, DateOnly purchasedate, int status,
        string supplierTitle, string? exchangeTitle, string? payment, DateOnly? etd,
        string? deliverterm, string? note);
    private sealed record PurchaseExportLineRow(
        Guid purchaseid, string productnum, string productTitle, decimal unitprice, int qty, decimal subtotal);
}
