using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台儀表板統計（唯讀，任一已登入管理員皆可讀，不綁特定模組權限）。
///   GET /admin/dashboard/stats — 一次回傳所有概覽數字
///
/// 時間以台灣時區（UTC+8）計算「今日／本月」，與寫入 createdate 的慣例一致。
/// </summary>
public sealed class DashboardAdminController
{
    // 低庫存門檻：上架商品全倉可售總量 ≤ 此值即列入警示（含 0=售完）。
    private const int LowStockThreshold = 10;

    private readonly IDbConnectionFactory _db;

    public DashboardAdminController(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IActionResult> Stats(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        // 以台灣時間決定今日／本月邊界，再以 UTC+8 的牆鐘值直接比對 createdate（亦為 UTC+8 寫入）。
        var nowTw       = DateTime.UtcNow.AddHours(8);
        var todayStart  = nowTw.Date;
        var tomorrow    = todayStart.AddDays(1);
        var monthStart  = new DateTime(nowTw.Year, nowTw.Month, 1);
        var nextMonth   = monthStart.AddMonths(1);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var row = await conn.QuerySingleAsync(@"
SELECT
  (SELECT COUNT(1) FROM Orders
     WHERE createdate >= @todayStart AND createdate < @tomorrow) AS todayOrders,
  (SELECT COUNT(1) FROM Orders
     WHERE deliverstatus IN (0, 4)) AS pendingShipment,
  (SELECT COUNT(1) FROM Orders
     WHERE paystatus = 0 AND deliverstatus <> 3) AS unpaidOrders,
  (SELECT COUNT(1) FROM Orders
     WHERE createdate >= @monthStart AND createdate < @nextMonth) AS monthOrders,
  (SELECT ISNULL(SUM(total), 0) FROM Orders
     WHERE paystatus = 1 AND createdate >= @monthStart AND createdate < @nextMonth) AS monthRevenue,
  (SELECT COUNT(1) FROM Products WHERE isdisabled = 0) AS activeProducts,
  (SELECT COUNT(1) FROM Members WHERE isenable = 1) AS activeMembers,
  (SELECT COUNT(*) FROM (
       SELECT p.productid
       FROM Products p
       LEFT JOIN Purchasedetails pd ON pd.productid = p.productid
       LEFT JOIN Stocks s            ON s.purchasedetailid = pd.purchasedetailid
       LEFT JOIN Warehousestocks ws  ON ws.stockid = s.stockid
       WHERE p.isdisabled = 0
       GROUP BY p.productid
       HAVING ISNULL(SUM(ws.quantity_left), 0) <= @lowStockThreshold
   ) ls) AS lowStock",
            new { todayStart, tomorrow, monthStart, nextMonth, lowStockThreshold = LowStockThreshold });

        return ctx.Ok(new
        {
            todayOrders     = (int)row.todayOrders,
            pendingShipment = (int)row.pendingShipment,
            unpaidOrders    = (int)row.unpaidOrders,
            monthOrders     = (int)row.monthOrders,
            monthRevenue    = (int)row.monthRevenue,
            activeProducts  = (int)row.activeProducts,
            activeMembers   = (int)row.activeMembers,
            lowStock        = (int)row.lowStock,
            lowStockThreshold = LowStockThreshold,
        });
    }
}
