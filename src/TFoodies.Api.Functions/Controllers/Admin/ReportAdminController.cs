using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台報表（ReportMs 模組）。
///   GET /admin/reports/sales?year=&amp;month=       — 依月份商品銷售量匯總
///   GET /admin/reports/amounts?startDate=&amp;endDate=&amp;payStatus=
///                                                — 依日期區間訂單金額統計
/// </summary>
public sealed class ReportAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public ReportAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db    = db;
    }

    // ══════════════════════════════════════════════════════════════════
    // SALES — GET /admin/reports/sales?year=2026&month=6
    // 依月份查已出貨訂單（deliverstatus=3），按商品匯總銷售量
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Sales(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ReportMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;

        if (!int.TryParse(q["year"],  out var year)  || year  < 2000 || year  > 2100)
            return ctx.BadRequest("year 為必填，且須為合法西元年（2000–2100）。");
        if (!int.TryParse(q["month"], out var month) || month < 1    || month > 12)
            return ctx.BadRequest("month 為必填，且須為 1–12 的整數。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var items = await conn.QueryAsync(@"
SELECT od.productid                    AS productId,
       p.title,
       p.capacity,
       SUM(od.qty)                     AS totalQty,
       SUM(od.qty * od.price)          AS totalAmount
FROM Orders o
JOIN Orderdetails od ON od.orderid = o.orderid
JOIN Products     p  ON p.productid = od.productid
WHERE YEAR(o.orderdate)  = @year
  AND MONTH(o.orderdate) = @month
  AND o.deliverstatus    = 3
GROUP BY od.productid, p.title, p.capacity
ORDER BY totalQty DESC",
            new { year, month });

        return ctx.Ok(new
        {
            year,
            month,
            items,
        });
    }

    // ══════════════════════════════════════════════════════════════════
    // AMOUNTS — GET /admin/reports/amounts?startDate=&endDate=&payStatus=
    // 依日期區間查訂單金額統計（deliverstatus IN (3,4)）
    // ══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Amounts(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ReportMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;

        if (!DateTime.TryParse(q["startDate"], out var startDate))
            return ctx.BadRequest("startDate 為必填，格式：yyyy-MM-dd。");
        if (!DateTime.TryParse(q["endDate"],   out var endDate))
            return ctx.BadRequest("endDate 為必填，格式：yyyy-MM-dd。");
        if (endDate < startDate)
            return ctx.BadRequest("endDate 不可早於 startDate。");

        // payStatus 為選填；僅在有值且為合法整數時才加入篩選
        int? payStatusFilter = int.TryParse(q["payStatus"], out var ps) ? ps : null;

        // endDate 含當天全天：補上 23:59:59
        var endDateInclusive = endDate.Date.AddDays(1).AddSeconds(-1);

        var payClause = payStatusFilter.HasValue ? "AND o.paystatus = @payStatus" : "";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var orders = (await conn.QueryAsync(
            $@"SELECT o.orderid,
                      o.ordercode,
                      o.orderdate,
                      o.total,
                      o.freight,
                      o.discount,
                      o.paystatus,
                      o.deliverstatus,
                      m.name AS memberName
               FROM Orders o
               JOIN Members m ON m.memberid = o.memberid
               WHERE o.orderdate >= @startDate
                 AND o.orderdate <= @endDate
                 AND o.deliverstatus IN (3, 4)
                 {payClause}
               ORDER BY o.orderdate",
            payStatusFilter.HasValue
                ? (object)new { startDate, endDate = endDateInclusive, payStatus = payStatusFilter.Value }
                : new { startDate, endDate = endDateInclusive }
        )).ToList();

        // 匯總統計
        var totalOrders   = orders.Count;
        var totalAmount   = orders.Sum(o => (decimal)((dynamic)o).total);
        var totalFreight  = orders.Sum(o => (decimal)((dynamic)o).freight);
        var totalDiscount = orders.Sum(o => (decimal)((dynamic)o).discount);

        return ctx.Ok(new
        {
            totalOrders,
            totalAmount,
            totalFreight,
            totalDiscount,
            orders,
        });
    }
}
