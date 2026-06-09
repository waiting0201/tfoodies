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
       m.name AS memberName, o.ordercode,
       r.note, r.receivestatus, r.refundstatus, r.warehousestatus
FROM Returns r
JOIN Members m ON m.memberid = r.memberid
JOIN Orders o ON o.orderid = r.orderid
WHERE {where}
ORDER BY r.returndate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Cast<object>().ToList();
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

SELECT rd.returndetailid, rd.orderdetailid, rd.qty, rd.note,
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

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ReturnRow(Guid returnid, Guid memberid, int refundstatus);
    private sealed record RefundRequest(string? Note);
}
