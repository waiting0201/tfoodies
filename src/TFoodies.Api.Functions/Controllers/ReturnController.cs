using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 前台退貨端點。
///   POST /store/returns                         — 建立退貨申請
///   GET  /member/returns?page=&amp;pageSize=     — 會員退貨列表（JWT required）
///   GET  /member/returns/{returnCode}           — 退貨明細（JWT required）
/// </summary>
public sealed class ReturnController
{
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;

    public ReturnController(IDbConnectionFactory db, ICodeNumberService codes)
    {
        _db = db;
        _codes = codes;
    }

    // POST /store/returns
    public async Task<IActionResult> CreateReturn(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<CreateReturnRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.OrderCode))
            return ctx.BadRequest("缺少 orderCode 欄位。");
        if (body.Items is null || body.Items.Count == 0)
            return ctx.BadRequest("退貨明細不能為空。");

        var memberId = ExtractMemberId(ctx.CurrentUser);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);

        // 驗證訂單存在且已出貨（deliverstatus=1）
        var order = await conn.QuerySingleOrDefaultAsync<OrderStatusRow>(
            "SELECT orderid, memberid, deliverstatus FROM Orders WHERE ordercode = @code",
            new { code = body.OrderCode });

        if (order is null) return ctx.NotFound("找不到訂單。");
        if (order.deliverstatus != 1)
            return ctx.UnprocessableEntity("只有已出貨的訂單才能申請退貨。");

        // 若有 JWT，驗證訂單屬於該會員
        if (memberId.HasValue && order.memberid != memberId.Value)
            return ctx.Forbidden("無法對其他會員的訂單申請退貨。");

        var effectiveMemberId = memberId ?? order.memberid;

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var returnCode = await _codes.NextAsync(CodeKind.Return, today, tx, ct);
            var returnId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Returns (returnid, memberid, orderid, returncode, returndate, note,
    receivestatus, refundstatus, warehousestatus)
VALUES (@returnid, @memberid, @orderid, @returncode, @returndate, @note, 0, 0, 0)",
                new
                {
                    returnid = returnId,
                    memberid = effectiveMemberId,
                    orderid = order.orderid,
                    returncode = returnCode,
                    returndate = today,
                    note = body.Note,
                }, tx);

            foreach (var item in body.Items)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Returndetails (returndetailid, returnid, orderdetailid, qty, note)
VALUES (NEWID(), @returnid, @orderdetailid, @qty, @note)",
                    new
                    {
                        returnid = returnId,
                        orderdetailid = item.OrderDetailId,
                        qty = item.Qty,
                        note = item.Note,
                    }, tx);
            }

            tx.Commit();
            return ctx.Created(new { returnId, returnCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // GET /member/returns?page=1&pageSize=20
    public async Task<IActionResult> ListMemberReturns(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var q = ctx.Request.Query;
        var page = Math.Max(1, int.TryParse(q["page"], out var p) ? p : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Returns WHERE memberid = @memberId",
            new { memberId });

        var items = await conn.QueryAsync(@"
SELECT r.returnid, r.returncode, r.returndate,
       o.ordercode, r.note,
       r.receivestatus, r.refundstatus, r.warehousestatus
FROM Returns r
JOIN Orders o ON o.orderid = r.orderid
WHERE r.memberid = @memberId
ORDER BY r.returndate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { memberId, offset, pageSize });

        var list = items.Cast<object>().ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /member/returns/{returnCode}
    public async Task<IActionResult> GetMemberReturn(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var returnCode = ctx.RequirePathParam("returnCode");
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT r.returnid, r.returncode, r.returndate,
       o.ordercode, r.note,
       r.receivestatus, r.refundstatus, r.warehousestatus
FROM Returns r
JOIN Orders o ON o.orderid = r.orderid
WHERE r.returncode = @returnCode AND r.memberid = @memberId;

SELECT rd.returndetailid, rd.orderdetailid, rd.qty, rd.note,
       od.price, od.subtotal,
       p.title AS productTitle, p.productid
FROM Returndetails rd
JOIN Orderdetails od ON od.orderdetailid = rd.orderdetailid
JOIN Products p ON p.productid = od.productid
JOIN Returns r2 ON r2.returnid = rd.returnid
WHERE r2.returncode = @returnCode AND r2.memberid = @memberId;",
            new { returnCode, memberId });

        var header = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (header is null) return ctx.NotFound("找不到退貨記錄。");

        var details = (await multi.ReadAsync<dynamic>()).ToList();
        return ctx.Ok(new { @return = header, details });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Guid? RequireMemberId(RouteContext ctx)
    {
        var user = ctx.CurrentUser;
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    private static Guid? ExtractMemberId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record CreateReturnRequest(
        string? OrderCode,
        List<ReturnItemRequest>? Items,
        string? Note);

    private sealed record ReturnItemRequest(
        Guid OrderDetailId,
        int Qty,
        string? Note);

    private sealed record OrderStatusRow(Guid orderid, Guid memberid, int deliverstatus);
}
