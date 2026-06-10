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
/// 後台退貨管理。
///   GET   /admin/returns?receivestatus=&amp;page=&amp;pageSize=  — 退貨列表
///   GET   /admin/returns/{id}                              — 退貨明細
///   PATCH /admin/returns/{id}/receive                      — 標記已收到退貨品
///   PATCH /admin/returns/{id}/refund                       — 執行退款（建立 Refounds 紀錄）
/// </summary>
public sealed class ReturnAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;

    public ReturnAdminController(
        IAdminPermissionService perms, IDbConnectionFactory db, ICodeNumberService codes)
    {
        _perms = perms;
        _db = db;
        _codes = codes;
    }

    // 舊系統 AddReturns 為每筆 returndetail 寫死的退貨會計科目 ID。
    private static readonly Guid ReturnAccountingId = new("469AF577-AC6F-4026-AD48-8918525D1ACF");

    // GET /admin/returns?receivestatus=&page=1&pageSize=20
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? receiveStatus = int.TryParse(q["receivestatus"], out var rs) ? rs : null;
        var page = Math.Max(1, int.TryParse(q["page"], out var p) ? p : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var clauses = new List<string>();
        var parms = new Dictionary<string, object?>();
        if (receiveStatus.HasValue)
        {
            clauses.Add("r.receivestatus = @rs");
            parms["rs"] = receiveStatus.Value;
        }
        var where = clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Returns r WHERE {where}", parms);

        var dp = new DynamicParameters(parms);
        dp.Add("offset", offset);
        dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync($@"
SELECT r.returnid, r.returncode, r.returndate,
       m.name AS memberName, m.mobile AS memberMobile, o.ordercode,
       r.note, r.receivestatus, r.refundstatus, r.warehousestatus
FROM Returns r
JOIN Members m ON m.memberid = r.memberid
JOIN Orders o ON o.orderid = r.orderid
WHERE {where}
ORDER BY r.receivedate DESC, r.returncode DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        // DapperRow key 不套 camelCase 政策，需投影到 anonymous object 並 .ToList()
        var list = items.Select(r => (object)new
        {
            returnId = r.returnid,
            returnCode = r.returncode,
            orderCode = r.ordercode,
            returnDate = r.returndate,
            r.memberName,
            r.memberMobile,
            refundStatus = r.refundstatus,
            receiveStatus = r.receivestatus,
            warehouseStatus = r.warehousestatus,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/returns/{id}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var returnId))
            return ctx.BadRequest("無效的退貨 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT r.returnid, r.returncode, r.returndate,
       m.memberid, m.name AS memberName, m.mobile AS memberMobile,
       o.orderid, o.ordercode,
       r.note, r.receivestatus, r.refundstatus, r.warehousestatus
FROM Returns r
JOIN Members m ON m.memberid = r.memberid
JOIN Orders o ON o.orderid = r.orderid
WHERE r.returnid = @returnId;

SELECT rd.returndetailid, rd.orderdetailid, rd.qty,
       od.price, od.subtotal,
       p.title AS productTitle, p.productid
FROM Returndetails rd
JOIN Orderdetails od ON od.orderdetailid = rd.orderdetailid
JOIN Products p ON p.productid = od.productid
WHERE rd.returnid = @returnId;",
            new { returnId });

        var header = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (header is null) return ctx.NotFound("找不到退貨記錄。");

        var details = (await multi.ReadAsync<dynamic>()).ToList();
        return ctx.Ok(new { @return = header, details });
    }

    // PATCH /admin/returns/{id}/receive — 標記已收到退貨品
    public async Task<IActionResult> Receive(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var returnId))
            return ctx.BadRequest("無效的退貨 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.ExecuteAsync(
            "UPDATE Returns SET receivestatus=1 WHERE returnid=@returnId AND receivestatus=0",
            new { returnId });

        if (rows == 0) return ctx.UnprocessableEntity("退貨記錄不存在或已標記為已收到。");
        return ctx.Ok(new { message = "已標記為已收到退貨品" });
    }

    // PATCH /admin/returns/{id}/refund — 執行退款
    public async Task<IActionResult> Refund(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var returnId))
            return ctx.BadRequest("無效的退貨 ID。");

        var body = await ctx.TryReadBodyAsync<RefundRequest>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var ret = await conn.QuerySingleOrDefaultAsync<ReturnRow>(
                "SELECT returnid, memberid, refundstatus FROM Returns WHERE returnid=@returnId",
                new { returnId }, tx);

            if (ret is null) return ctx.NotFound("找不到退貨記錄。");
            if (ret.refundstatus == 1) return ctx.UnprocessableEntity("此退貨單已完成退款。");

            // 計算退款金額：退貨明細 qty * 訂單明細單價
            var amount = await conn.ExecuteScalarAsync<int>(@"
SELECT ISNULL(SUM(rd.qty * od.price), 0)
FROM Returndetails rd
JOIN Orderdetails od ON od.orderdetailid = rd.orderdetailid
WHERE rd.returnid = @returnId",
                new { returnId }, tx);

            var refoundCode = await _codes.NextAsync(CodeKind.Refound, today, tx, ct);
            var refoundId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Refounds (refoundid, memberid, returnid, refoundcode, amount, note, createdate)
VALUES (@refoundid, @memberid, @returnid, @refoundcode, @amount, @note, @createdate)",
                new
                {
                    refoundid = refoundId,
                    memberid = ret.memberid,
                    returnid = returnId,
                    refoundcode = refoundCode,
                    amount,
                    note = body?.Note,
                    createdate = now,
                }, tx);

            await conn.ExecuteAsync(
                "UPDATE Returns SET refundstatus=1 WHERE returnid=@returnId",
                new { returnId }, tx);

            tx.Commit();
            return ctx.Ok(new { refoundCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // POST /admin/returns — 後台手動建立退貨單（對應舊系統 AddReturns）。
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertReturnRequest>();
        if (body is null || body.OrderId == Guid.Empty) return ctx.BadRequest("請選擇訂單。");
        var details = (body.Details ?? new()).Where(d => d.Qty > 0).ToList();
        if (details.Count == 0) return ctx.BadRequest("請至少填寫一項退貨數量。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var memberId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT memberid FROM Orders WHERE orderid=@orderId", new { orderId = body.OrderId }, tx);
            if (memberId is null) { tx.Rollback(); return ctx.NotFound("找不到訂單。"); }

            var returnId = Guid.NewGuid();
            var returnCode = await _codes.NextAsync(CodeKind.Return, today, tx, ct);

            await conn.ExecuteAsync(@"
INSERT INTO Returns (returnid, memberid, orderid, returncode, returndate,
    receivestatus, receivedate, refundstatus, refunddate, createdate, warehousestatus, note)
VALUES (@returnid, @memberid, @orderid, @returncode, @returndate,
    @receivestatus, @receivedate, @refundstatus, NULL, @createdate, @warehousestatus, @note)",
                new
                {
                    returnid = returnId,
                    memberid = memberId.Value,
                    orderid = body.OrderId,
                    returncode = returnCode,
                    returndate = body.ReturnDate ?? today,
                    receivestatus = body.ReceiveStatus,
                    receivedate = body.ReceiveDate,
                    refundstatus = body.RefundStatus,
                    createdate = today,
                    warehousestatus = (int)Domain.Enums.WarehouseStatus.NotStockedIn,
                    note = body.Note,
                }, tx);

            foreach (var d in details)
                await InsertReturnDetailAsync(conn, tx, returnId, d.OrderDetailId, d.Qty);

            tx.Commit();
            return ctx.Created(new { returnCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/returns/{id} — 編輯退貨單（對應舊系統 EditReturns，含明細差異更新）。
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "OrderMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var returnId)) return ctx.BadRequest("無效的退貨 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertReturnRequest>();
        if (body is null) return ctx.BadRequest("請求內容無效。");
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var rows = await conn.ExecuteAsync(@"
UPDATE Returns SET returndate=@returndate, receivestatus=@receivestatus, receivedate=@receivedate,
    refundstatus=@refundstatus, refunddate=@refunddate, note=@note
WHERE returnid=@returnId",
                new
                {
                    returnId,
                    returndate = body.ReturnDate,
                    receivestatus = body.ReceiveStatus,
                    receivedate = body.ReceiveDate,
                    refundstatus = body.RefundStatus,
                    refunddate = body.RefundDate,
                    note = body.Note,
                }, tx);
            if (rows == 0) { tx.Rollback(); return ctx.NotFound("找不到退貨記錄。"); }

            var details = (body.Details ?? new()).Where(d => d.Qty > 0).ToList();

            var existing = (await conn.QueryAsync<Guid>(
                "SELECT returndetailid FROM Returndetails WHERE returnid=@returnId",
                new { returnId }, tx)).ToHashSet();
            var keep = details.Where(d => d.ReturnDetailId.HasValue).Select(d => d.ReturnDetailId!.Value).ToHashSet();

            foreach (var delId in existing.Where(e => !keep.Contains(e)))
                await conn.ExecuteAsync("DELETE FROM Returndetails WHERE returndetailid=@delId", new { delId }, tx);

            foreach (var d in details)
            {
                if (d.ReturnDetailId.HasValue && existing.Contains(d.ReturnDetailId.Value))
                    await conn.ExecuteAsync(
                        "UPDATE Returndetails SET orderdetailid=@odid, qty=@qty WHERE returndetailid=@id",
                        new { odid = d.OrderDetailId, qty = d.Qty, id = d.ReturnDetailId.Value }, tx);
                else
                    await InsertReturnDetailAsync(conn, tx, returnId, d.OrderDetailId, d.Qty);
            }

            tx.Commit();
            return ctx.Ok(new { message = "退貨單已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    private static async Task InsertReturnDetailAsync(
        SqlConnection conn, SqlTransaction tx, Guid returnId, Guid orderDetailId, int qty)
    {
        var price = await conn.ExecuteScalarAsync<int?>(
            "SELECT price FROM Orderdetails WHERE orderdetailid=@id", new { id = orderDetailId }, tx) ?? 0;
        await conn.ExecuteAsync(@"
INSERT INTO Returndetails (returndetailid, returnid, orderdetailid, accountingid, qty, price)
VALUES (@id, @returnid, @orderdetailid, @accountingid, @qty, @price)",
            new
            {
                id = Guid.NewGuid(),
                returnid = returnId,
                orderdetailid = orderDetailId,
                accountingid = ReturnAccountingId,
                qty,
                price,
            }, tx);
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ReturnRow(Guid returnid, Guid memberid, int refundstatus);
    private sealed record RefundRequest(string? Note);

    private sealed record UpsertReturnRequest(
        Guid OrderId, DateOnly? ReturnDate,
        int ReceiveStatus, DateOnly? ReceiveDate,
        int RefundStatus, DateOnly? RefundDate,
        string? Note, List<ReturnDetailItem>? Details);

    private sealed record ReturnDetailItem(Guid? ReturnDetailId, Guid OrderDetailId, int Qty);
}
