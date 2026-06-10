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
    // 比照舊 Salereports：依訂單月份匯總商品銷售「數量」。
    //   • 計入已出貨(1) / 退貨(2) 之訂單（EnumDeliverStatus：已出貨=1, 退貨=2）。
    //   • 套組商品（Products.isset=1）展開為 Setproducts 子品項；子品項以每套
    //     定義數量 sp.qty 計入（每筆明細加一次，不乘訂單數量，與舊系統一致）。
    //   • 非套組商品以實際訂購數量 od.qty 計入。
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

        const string sql = @"
WITH detail AS (
    -- 非套組：實際訂購數量
    SELECT od.productid AS productId, SUM(od.qty) AS qty
    FROM Orders o
    JOIN Orderdetails od ON od.orderid = o.orderid
    JOIN Products     p  ON p.productid = od.productid
    WHERE YEAR(o.orderdate) = @year AND MONTH(o.orderdate) = @month
      AND o.deliverstatus IN (1, 2)
      AND p.isset = 0
    GROUP BY od.productid

    UNION ALL

    -- 套組：展開為 Setproducts 子品項，子品項以每套定義數量計入
    SELECT sp.productid AS productId, SUM(sp.qty) AS qty
    FROM Orders o
    JOIN Orderdetails od ON od.orderid = o.orderid
    JOIN Products     p  ON p.productid  = od.productid
    JOIN Setproducts  sp ON sp.oproductid = od.productid
    WHERE YEAR(o.orderdate) = @year AND MONTH(o.orderdate) = @month
      AND o.deliverstatus IN (1, 2)
      AND p.isset = 1
    GROUP BY sp.productid
)
SELECT d.productId,
       LTRIM(RTRIM(pr.title + ' ' + ISNULL(pr.capacity, ''))) AS name,
       SUM(d.qty) AS qty
FROM detail d
JOIN Products pr ON pr.productid = d.productId
GROUP BY d.productId, pr.title, pr.capacity
ORDER BY qty DESC";

        var items = await conn.QueryAsync(sql, new { year, month });

        return ctx.Ok(new
        {
            year,
            month,
            items,
        });
    }

    // ══════════════════════════════════════════════════════════════════
    // AMOUNTS — GET /admin/reports/amounts?startDate=&endDate=&payStatus=
    // 比照舊 Amountreports：依日期區間列出訂單明細（起訖日必填、付款狀態選填）。
    //   • 計入已出貨(1) / 退貨(2) 之訂單。
    //   • 總金額 = 運費 freight + 商品金額 total − 折扣 discount。
    //   • 回傳 grandTotal（總計）+ orders（明細）。
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
            $@"SELECT o.ordercode,
                      o.ordertype,
                      o.orderdate,
                      o.paytype,
                      o.paystatus,
                      (o.freight + o.total - ISNULL(o.discount, 0)) AS amount,
                      o.recivername,
                      w.title  AS warehouseTitle,
                      m.name   AS memberName,
                      m.mobile AS memberMobile
               FROM Orders o
               JOIN Members m ON m.memberid = o.memberid
               LEFT JOIN Warehouses w ON w.warehouseid = o.warehouseid
               WHERE o.orderdate >= @startDate
                 AND o.orderdate <= @endDate
                 AND o.deliverstatus IN (1, 2)
                 {payClause}
               ORDER BY o.orderdate, o.ordercode",
            payStatusFilter.HasValue
                ? (object)new { startDate, endDate = endDateInclusive, payStatus = payStatusFilter.Value }
                : new { startDate, endDate = endDateInclusive }
        )).ToList();

        var grandTotal = orders.Sum(o => (int)((dynamic)o).amount);

        return ctx.Ok(new
        {
            grandTotal,
            orders,
        });
    }
}
