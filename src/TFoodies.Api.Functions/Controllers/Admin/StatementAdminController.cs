using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台會計報表管理（對應 Lim 群組 StatementMs，LimID 52 的 2 個子模組）。
///
/// 舊系統 StatementMsController 僅有一個空白的損益表篩選表單（起訖日期），
/// 沒有任何計算邏輯，資產負債表則完全未實作。本控制器依現有交易資料重新實作：
///
///   ── 損益表 Income Statement（依日期區間彙總） ──
///   GET /admin/statements/income-statement?startDate=&endDate=
///     · 營業收入：Invoicedetails.price（未稅淨額）依會計科目彙總，篩 Invoices.requestdate
///     · 銷貨退回：Returndetails.price 依會計科目彙總，篩 Returns.returndate
///     · 營業支出：Expendituredetails.price 依會計科目彙總，篩 Expenditures.expendituredate
///     · 本期損益 = 營業收入 − 銷貨退回 − 營業支出
///
///   ── 資產負債表 Balance Sheet（截至某日的推導快照） ──
///   GET /admin/statements/balance-sheet?asOf=
///     現行 schema 無資產／負債／權益科目表，故由交易資料推導：
///     · 現金及約當現金 = 累計收款(Incomes) − 累計付款(Outcomes) − 累計退款(Refounds)
///     · 應收帳款       = 尚未收款(incomeid IS NULL)之請款單含稅總額
///     · 期末存貨（現值）= Σ 各倉現有量(quantity_left) × 進貨單價(unitprice)
///     · 應付帳款       = 截至 asOf 之應付憑單金額 − 已付款金額
///     · 業主權益（淨值）= 資產總額 − 負債總額（差額平衡項）
/// </summary>
public sealed class StatementAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public StatementAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms; _db = db;
    }

    private CancellationToken Ct(RouteContext ctx) => ctx.Request.HttpContext.RequestAborted;

    // GET /admin/statements/income-statement?startDate=2026-01-01&endDate=2026-06-10
    public async Task<IActionResult> IncomeStatement(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "StatementMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!DateOnly.TryParse(ctx.Request.Query["startDate"], out var start))
            return ctx.BadRequest("缺少或無效的 startDate。");
        if (!DateOnly.TryParse(ctx.Request.Query["endDate"], out var end))
            return ctx.BadRequest("缺少或無效的 endDate。");
        if (end < start) return ctx.BadRequest("結束日期不可早於起始日期。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));

        // 營業收入：請款明細（未稅淨額），依請款日落在區間
        var revenueRows = (await conn.QueryAsync(@"
SELECT COALESCE(a.accountingcode, '—') AS accountingCode,
       COALESCE(a.title, N'未分類') AS accountingTitle,
       SUM(id2.price) AS amount
FROM Invoicedetails id2
JOIN Invoices i ON i.invoiceid = id2.invoiceid
LEFT JOIN Accountings a ON a.accountingid = id2.accountingid
WHERE CAST(i.requestdate AS date) BETWEEN @start AND @end
GROUP BY a.accountingcode, a.title
ORDER BY a.accountingcode", new { start, end })).ToList();

        // 銷貨退回：退貨明細，依退貨日落在區間
        var returnRows = (await conn.QueryAsync(@"
SELECT COALESCE(a.accountingcode, '—') AS accountingCode,
       COALESCE(a.title, N'銷貨退回') AS accountingTitle,
       SUM(rd.price) AS amount
FROM Returndetails rd
JOIN Returns r ON r.returnid = rd.returnid
LEFT JOIN Accountings a ON a.accountingid = rd.accountingid
WHERE CAST(r.returndate AS date) BETWEEN @start AND @end
GROUP BY a.accountingcode, a.title
ORDER BY a.accountingcode", new { start, end })).ToList();

        // 營業支出：應付憑單明細，依支出日落在區間
        var expenseRows = (await conn.QueryAsync(@"
SELECT COALESCE(a.accountingcode, '—') AS accountingCode,
       COALESCE(a.title, N'未分類') AS accountingTitle,
       SUM(ed.price) AS amount
FROM Expendituredetails ed
JOIN Expenditures e ON e.expenditureid = ed.expenditureid
LEFT JOIN Accountings a ON a.accountingid = ed.accountingid
WHERE CAST(e.expendituredate AS date) BETWEEN @start AND @end
GROUP BY a.accountingcode, a.title
ORDER BY a.accountingcode", new { start, end })).ToList();

        static List<object> Project(List<dynamic> rows) => rows.Select(r => (object)new
        {
            accountingCode = (string)r.accountingCode,
            accountingTitle = (string)r.accountingTitle,
            amount = (int)r.amount,
        }).ToList();

        long revenueTotal = revenueRows.Sum(r => (long)(int)r.amount);
        long returnTotal = returnRows.Sum(r => (long)(int)r.amount);
        long expenseTotal = expenseRows.Sum(r => (long)(int)r.amount);

        return ctx.Ok(new
        {
            startDate = start,
            endDate = end,
            revenues = Project(revenueRows),
            revenueTotal,
            returns = Project(returnRows),
            returnTotal,
            expenses = Project(expenseRows),
            expenseTotal,
            netRevenue = revenueTotal - returnTotal,
            netIncome = revenueTotal - returnTotal - expenseTotal,
        });
    }

    // GET /admin/statements/balance-sheet?asOf=2026-06-10
    public async Task<IActionResult> BalanceSheet(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "StatementMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!DateOnly.TryParse(ctx.Request.Query["asOf"], out var asOf))
            return ctx.BadRequest("缺少或無效的 asOf。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));

        var row = await conn.QuerySingleAsync(@"
SELECT
  -- 現金：累計收款 − 累計付款 − 累計退款
  ISNULL((SELECT SUM(amount) FROM Incomes  WHERE CAST(incomedate   AS date) <= @asOf), 0)
  - ISNULL((SELECT SUM(amount) FROM Outcomes WHERE CAST(outcomedate AS date) <= @asOf), 0)
  - ISNULL((SELECT SUM(amount) FROM Refounds WHERE CAST(refounddate AS date) <= @asOf), 0) AS cash,
  -- 應收帳款：尚未收款的請款單含稅總額（請款日 <= asOf）
  ISNULL((SELECT SUM(id2.price + ISNULL(id2.tax, 0))
          FROM Invoicedetails id2
          JOIN Invoices i ON i.invoiceid = id2.invoiceid
          WHERE i.incomeid IS NULL AND CAST(i.requestdate AS date) <= @asOf), 0) AS accountsReceivable,
  -- 期末存貨（現值）：各倉現有量 × 進貨單價
  ISNULL((SELECT SUM(CAST(ws.quantity_left AS decimal(18,2)) * pd.unitprice)
          FROM Warehousestocks ws
          JOIN Stocks s ON s.stockid = ws.stockid
          JOIN Purchasedetails pd ON pd.purchasedetailid = s.purchasedetailid
          WHERE ws.quantity_left > 0), 0) AS inventory,
  -- 應付帳款：截至 asOf 之應付憑單金額 − 已付款金額
  ISNULL((SELECT SUM(ed.price)
          FROM Expendituredetails ed
          JOIN Expenditures e ON e.expenditureid = ed.expenditureid
          WHERE CAST(e.expendituredate AS date) <= @asOf), 0)
  - ISNULL((SELECT SUM(oc.amount)
          FROM Outcomes oc
          JOIN Expenditures e ON e.expenditureid = oc.expenditureid
          WHERE CAST(e.expendituredate AS date) <= @asOf
            AND CAST(oc.outcomedate AS date) <= @asOf), 0) AS accountsPayable
", new { asOf });

        long cash = (int)row.cash;
        long accountsReceivable = (int)row.accountsReceivable;
        long inventory = (long)Math.Round((decimal)row.inventory);
        long accountsPayable = (int)row.accountsPayable;

        long assetsTotal = cash + accountsReceivable + inventory;
        long liabilitiesTotal = accountsPayable;
        long equityTotal = assetsTotal - liabilitiesTotal;

        return ctx.Ok(new
        {
            asOf,
            assets = new
            {
                cash,
                accountsReceivable,
                inventory,
                total = assetsTotal,
            },
            liabilities = new
            {
                accountsPayable,
                total = liabilitiesTotal,
            },
            equity = new
            {
                retainedEarnings = equityTotal,
                total = equityTotal,
            },
        });
    }
}
