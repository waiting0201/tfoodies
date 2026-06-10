using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台會計帳管理（移植舊系統 AccountingMsController，對應 Lim 群組 AccountingMs 的 7 個子模組）。
///
///   ── 匯率 Exchanges（GET 在 PurchaseAdminController，維護歸此） ──
///   POST   /admin/exchanges                       — 新增幣別
///   PUT    /admin/exchanges/{id}                   — 編輯幣別
///   DELETE /admin/exchanges/{id}                   — 刪除幣別（被採購單引用則拒絕）
///
///   ── 會計科目 Accountings ──
///   GET    /admin/accountings                      — 科目清單
///   POST   /admin/accountings                      — 新增科目
///   PUT    /admin/accountings/{id}                  — 編輯科目
///   DELETE /admin/accountings/{id}                  — 刪除科目（被明細引用則拒絕）
///
///   ── 營業支出 Expenditures（應付） ──
///   GET    /admin/expenditures                      — 應付憑單列表
///   GET    /admin/expenditures/payable              — 未結清憑單（付款用）
///   GET    /admin/expenditures/{id}                 — 應付憑單明細
///   POST   /admin/expenditures                      — 新增手動憑單
///   PUT    /admin/expenditures/{id}                 — 編輯憑單（明細 diff，重算狀態）
///   DELETE /admin/expenditures/{id}                 — 刪除憑單
///
///   ── 付款 Outcomes ──
///   GET    /admin/outcomes                          — 付款列表
///   POST   /admin/outcomes                          — 對憑單付款
///   PUT    /admin/outcomes/{id}                     — 編輯付款（重算憑單狀態）
///   DELETE /admin/outcomes/{id}                     — 刪除付款
///
///   ── 退款 Refounds ──
///   GET    /admin/refounds                          — 退款列表
///   GET    /admin/refounds/refundable-members       — 有待退款退貨單的會員
///   GET    /admin/refounds/refundable-returns       — 某會員待退款的退貨單
///   POST   /admin/refounds                          — 建立退款
///   PUT    /admin/refounds/{id}                     — 編輯退款
///   DELETE /admin/refounds/{id}                     — 刪除退款（還原退貨/訂單狀態）
///
///   ── 請款 AR Invoices（內部應收，非電子發票） ──
///   GET    /admin/ar-invoices                       — 請款單列表
///   GET    /admin/ar-invoices/billable-members      — 有未請款訂單的會員
///   GET    /admin/ar-invoices/billable-orders       — 某會員可請款（未付款且未請款）訂單
///   GET    /admin/ar-invoices/{id}                  — 請款單明細
///   POST   /admin/ar-invoices                       — 以多筆訂單建立請款單
///   PUT    /admin/ar-invoices/{id}                  — 編輯請款單表頭
///   DELETE /admin/ar-invoices/{id}                  — 刪除請款單
///
///   ── 入帳 Incomes（收款） ──
///   GET    /admin/incomes                           — 收款列表
///   GET    /admin/incomes/billable-members          — 有未收款請款單的會員
///   GET    /admin/incomes/billable-invoices         — 某會員未收款的請款單
///   GET    /admin/incomes/{id}                      — 收款明細
///   POST   /admin/incomes                           — 建立收款（標記訂單已付款）
///   PUT    /admin/incomes/{id}                      — 編輯收款
///   DELETE /admin/incomes/{id}                      — 刪除收款（還原訂單付款狀態）
/// </summary>
public sealed class AccountingAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly ICodeNumberService _codes;

    public AccountingAdminController(
        IAdminPermissionService perms, IDbConnectionFactory db, ICodeNumberService codes)
    {
        _perms = perms; _db = db; _codes = codes;
    }

    private CancellationToken Ct(RouteContext ctx) => ctx.Request.HttpContext.RequestAborted;

    private static (int page, int pageSize, int offset) Paging(RouteContext ctx)
    {
        var page = Math.Max(1, int.TryParse(ctx.Request.Query["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(ctx.Request.Query["pageSize"], out var sz) ? sz : 20, 1, 100);
        return (page, pageSize, (page - 1) * pageSize);
    }

    // ── 匯率 Exchanges（CUD；GET /admin/exchanges 在 PurchaseAdminController） ─────

    // POST /admin/exchanges
    public async Task<IActionResult> CreateExchange(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<ExchangeRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("幣別名稱為必填。");
        if (body.Rate <= 0) return ctx.BadRequest("匯率必須大於 0。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO Exchanges (exchangeid, title, rate) VALUES (@id, @title, @rate)",
            new { id, title = body.Title.Trim(), rate = body.Rate });
        return ctx.Created(new { exchangeId = id });
    }

    // PUT /admin/exchanges/{id}
    public async Task<IActionResult> UpdateExchange(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<ExchangeRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("幣別名稱為必填。");
        if (body.Rate <= 0) return ctx.BadRequest("匯率必須大於 0。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.ExecuteAsync(
            "UPDATE Exchanges SET title=@title, rate=@rate WHERE exchangeid=@id",
            new { id, title = body.Title.Trim(), rate = body.Rate });
        if (rows == 0) return ctx.NotFound("找不到幣別。");
        return ctx.Ok(new { message = "已更新" });
    }

    // DELETE /admin/exchanges/{id}
    public async Task<IActionResult> DeleteExchange(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var used = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Purchases WHERE exchangeid=@id", new { id });
        if (used > 0) return ctx.UnprocessableEntity("此幣別已被採購單引用，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Exchanges WHERE exchangeid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到幣別。");
        return ctx.Ok(new { message = "已刪除" });
    }

    // ── 會計科目 Accountings ───────────────────────────────────────────────────────

    // GET /admin/accountings
    public async Task<IActionResult> ListAccountings(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(
            "SELECT accountingid, accountingcode, title FROM Accountings ORDER BY accountingcode");
        var items = rows.Select(r => (object)new
        {
            accountingId = r.accountingid,
            accountingCode = r.accountingcode,
            r.title,
        }).ToList();
        return ctx.Ok(items);
    }

    // POST /admin/accountings
    public async Task<IActionResult> CreateAccounting(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<AccountingRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.AccountingCode) || string.IsNullOrWhiteSpace(body.Title))
            return ctx.BadRequest("科目代號與名稱為必填。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Accountings WHERE accountingcode=@code", new { code = body.AccountingCode.Trim() });
        if (dup > 0) return ctx.UnprocessableEntity("科目代號已存在。");

        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            "INSERT INTO Accountings (accountingid, accountingcode, title) VALUES (@id, @code, @title)",
            new { id, code = body.AccountingCode.Trim(), title = body.Title.Trim() });
        return ctx.Created(new { accountingId = id });
    }

    // PUT /admin/accountings/{id}
    public async Task<IActionResult> UpdateAccounting(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<AccountingRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.AccountingCode) || string.IsNullOrWhiteSpace(body.Title))
            return ctx.BadRequest("科目代號與名稱為必填。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Accountings WHERE accountingcode=@code AND accountingid<>@id",
            new { code = body.AccountingCode.Trim(), id });
        if (dup > 0) return ctx.UnprocessableEntity("科目代號已存在。");

        var rows = await conn.ExecuteAsync(
            "UPDATE Accountings SET accountingcode=@code, title=@title WHERE accountingid=@id",
            new { id, code = body.AccountingCode.Trim(), title = body.Title.Trim() });
        if (rows == 0) return ctx.NotFound("找不到會計科目。");
        return ctx.Ok(new { message = "已更新" });
    }

    // DELETE /admin/accountings/{id}
    public async Task<IActionResult> DeleteAccounting(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var used = await conn.ExecuteScalarAsync<int>(@"
SELECT
  (SELECT COUNT(1) FROM Expendituredetails WHERE accountingid=@id) +
  (SELECT COUNT(1) FROM Returndetails WHERE accountingid=@id) +
  (SELECT COUNT(1) FROM Invoicedetails WHERE accountingid=@id)", new { id });
        if (used > 0) return ctx.UnprocessableEntity("此會計科目已被使用，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Accountings WHERE accountingid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到會計科目。");
        return ctx.Ok(new { message = "已刪除" });
    }

    // ── 營業支出 Expenditures（應付） ──────────────────────────────────────────────

    // GET /admin/expenditures?status=&page=1&pageSize=20
    public async Task<IActionResult> ListExpenditures(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        int? status = int.TryParse(ctx.Request.Query["status"], out var s) ? s : null;
        var (page, pageSize, offset) = Paging(ctx);
        var where = status.HasValue ? "e.status = @status" : "1=1";

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var dp = new DynamicParameters();
        if (status.HasValue) dp.Add("status", status.Value);
        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM Expenditures e WHERE {where}", dp);

        dp.Add("offset", offset); dp.Add("pageSize", pageSize);
        var rows = await conn.QueryAsync($@"
SELECT e.expenditureid, e.expenditurecode, e.expendituredate, e.status, e.sourcetype, e.note,
       s.title AS supplierTitle,
       ISNULL((SELECT SUM(ed.price) FROM Expendituredetails ed WHERE ed.expenditureid=e.expenditureid),0) AS totalAmount,
       ISNULL((SELECT SUM(oc.amount) FROM Outcomes oc WHERE oc.expenditureid=e.expenditureid),0) AS paidAmount
FROM Expenditures e
LEFT JOIN Suppliers s ON s.supplierid=e.supplierid
WHERE {where}
ORDER BY e.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            expenditureId = r.expenditureid,
            expenditureCode = r.expenditurecode,
            expenditureDate = r.expendituredate,
            r.status,
            sourceType = r.sourcetype,
            r.note,
            supplierTitle = (string?)r.supplierTitle ?? "（手動）",
            r.totalAmount,
            r.paidAmount,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/expenditures/payable — 未結清憑單（付款選擇用）
    public async Task<IActionResult> ListPayableExpenditures(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT e.expenditureid, e.expenditurecode, e.expendituredate, e.status,
       s.title AS supplierTitle,
       ISNULL((SELECT SUM(ed.price) FROM Expendituredetails ed WHERE ed.expenditureid=e.expenditureid),0) AS totalAmount,
       ISNULL((SELECT SUM(oc.amount) FROM Outcomes oc WHERE oc.expenditureid=e.expenditureid),0) AS paidAmount
FROM Expenditures e
LEFT JOIN Suppliers s ON s.supplierid=e.supplierid
WHERE e.status <> 2
ORDER BY e.expendituredate DESC, e.expenditurecode DESC");

        var items = rows.Select(r => (object)new
        {
            expenditureId = r.expenditureid,
            expenditureCode = r.expenditurecode,
            expenditureDate = r.expendituredate,
            r.status,
            supplierTitle = (string?)r.supplierTitle ?? "（手動）",
            r.totalAmount,
            r.paidAmount,
        }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/expenditures/{id}
    public async Task<IActionResult> DetailExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var multi = await conn.QueryMultipleAsync(@"
SELECT e.expenditureid, e.expenditurecode, e.expendituredate, e.status, e.sourcetype, e.note, e.purchaseid,
       s.title AS supplierTitle
FROM Expenditures e LEFT JOIN Suppliers s ON s.supplierid=e.supplierid WHERE e.expenditureid=@eid;
SELECT ed.expendituredetailid, ed.accountingid, ed.summary, ed.price, ed.purchasedetailid,
       a.accountingcode, a.title AS accountingTitle
FROM Expendituredetails ed LEFT JOIN Accountings a ON a.accountingid=ed.accountingid
WHERE ed.expenditureid=@eid;
SELECT oc.outcomeid, oc.outcomecode, oc.outcomedate, oc.amount, oc.note FROM Outcomes oc
WHERE oc.expenditureid=@eid ORDER BY oc.outcomedate;", new { eid });

        var h = await multi.ReadSingleOrDefaultAsync();
        if (h is null) return ctx.NotFound("找不到應付憑單");
        var detailRows = (await multi.ReadAsync()).ToList();
        var outcomeRows = (await multi.ReadAsync()).ToList();

        var header = new
        {
            expenditureId = h.expenditureid,
            expenditureCode = h.expenditurecode,
            expenditureDate = h.expendituredate,
            h.status,
            sourceType = h.sourcetype,
            h.note,
            purchaseId = h.purchaseid,
            supplierTitle = (string?)h.supplierTitle ?? "（手動）",
        };
        var details = detailRows.Select(r => (object)new
        {
            expenditureDetailId = r.expendituredetailid,
            accountingId = r.accountingid,
            accountingCode = r.accountingcode,
            accountingTitle = r.accountingTitle,
            r.summary,
            r.price,
            purchaseDetailId = r.purchasedetailid,
        }).ToList();
        var outcomes = outcomeRows.Select(r => (object)new
        {
            outcomeId = r.outcomeid,
            outcomeCode = r.outcomecode,
            outcomeDate = r.outcomedate,
            r.amount,
            r.note,
        }).ToList();
        return ctx.Ok(new { expenditure = header, details, outcomes });
    }

    // POST /admin/expenditures — 手動新增
    public async Task<IActionResult> CreateExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateExpenditureRequest>();
        if (body is null || body.Lines is null || body.Lines.Count == 0)
            return ctx.BadRequest("明細不能為空。");
        if (body.Lines.Any(l => l.AccountingId is null || l.AccountingId == Guid.Empty))
            return ctx.BadRequest("每筆明細都必須選擇會計科目。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var expCode = await _codes.NextAsync(CodeKind.Expenditure, today, tx);
            var expId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Expenditures (expenditureid, supplierid, expenditurecode, expendituredate,
    sourcetype, status, note, createdate)
VALUES (@expId, @supplierid, @expCode, @date, 0, 0, @note, @now)",
                new { expId, supplierid = body.SupplierId, expCode,
                      date = body.ExpenditureDate ?? today, note = body.Note, now }, tx);

            foreach (var line in body.Lines)
            {
                await conn.ExecuteAsync(@"
INSERT INTO Expendituredetails (expendituredetailid, expenditureid, accountingid, price, summary)
VALUES (NEWID(), @expId, @accountingid, @price, @summary)",
                    new { expId, accountingid = line.AccountingId, price = line.Price, summary = line.Summary }, tx);
            }

            tx.Commit();
            return ctx.Created(new { expenditureId = expId, expenditureCode = expCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/expenditures/{id} — 編輯表頭 + 明細 diff，重算狀態
    public async Task<IActionResult> UpdateExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eid)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateExpenditureRequest>();
        if (body is null) return ctx.BadRequest("請求內容不正確。");
        if (body.Lines is null || body.Lines.Count == 0) return ctx.BadRequest("明細不能為空。");
        if (body.Lines.Any(l => l.AccountingId is null || l.AccountingId == Guid.Empty))
            return ctx.BadRequest("每筆明細都必須選擇會計科目。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Expenditures WHERE expenditureid=@eid", new { eid }, tx);
            if (exists == 0) { tx.Rollback(); return ctx.NotFound("找不到應付憑單。"); }

            await conn.ExecuteAsync(
                "UPDATE Expenditures SET expendituredate=@date, note=@note WHERE expenditureid=@eid",
                new { eid, date = body.ExpenditureDate, note = body.Note }, tx);

            // 刪除不在送出清單中的既有明細
            var keep = body.Lines.Where(l => l.ExpenditureDetailId.HasValue)
                                 .Select(l => l.ExpenditureDetailId!.Value).ToList();
            if (keep.Count == 0) keep.Add(Guid.Empty);
            await conn.ExecuteAsync(
                "DELETE FROM Expendituredetails WHERE expenditureid=@eid AND expendituredetailid NOT IN @keep",
                new { eid, keep }, tx);

            foreach (var l in body.Lines)
            {
                if (l.ExpenditureDetailId.HasValue)
                    await conn.ExecuteAsync(
                        "UPDATE Expendituredetails SET accountingid=@acc, price=@price, summary=@summary WHERE expendituredetailid=@did",
                        new { did = l.ExpenditureDetailId.Value, acc = l.AccountingId, price = l.Price, summary = l.Summary }, tx);
                else
                    await conn.ExecuteAsync(@"
INSERT INTO Expendituredetails (expendituredetailid, expenditureid, accountingid, price, summary)
VALUES (NEWID(), @eid, @acc, @price, @summary)",
                        new { eid, acc = l.AccountingId, price = l.Price, summary = l.Summary }, tx);
            }

            await RecalcExpenditureStatusAsync(conn, tx, eid);
            tx.Commit();
            return ctx.Ok(new { message = "已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/expenditures/{id}
    public async Task<IActionResult> DeleteExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        var outcomeCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Outcomes WHERE expenditureid=@eid", new { eid });
        if (outcomeCount > 0) return ctx.UnprocessableEntity("此應付憑單已有付款記錄，無法刪除。請先刪除付款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 若來自採購單，還原採購單轉支出旗標
            var purchaseId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT purchaseid FROM Expenditures WHERE expenditureid=@eid", new { eid }, tx);

            await conn.ExecuteAsync("DELETE FROM Expendituredetails WHERE expenditureid=@eid", new { eid }, tx);
            var rows = await conn.ExecuteAsync("DELETE FROM Expenditures WHERE expenditureid=@eid", new { eid }, tx);
            if (rows == 0) { tx.Rollback(); return ctx.NotFound("找不到應付憑單。"); }

            if (purchaseId is not null)
                await conn.ExecuteAsync("UPDATE Purchases SET isexpenditure=0 WHERE purchaseid=@pid",
                    new { pid = purchaseId }, tx);

            tx.Commit();
            return ctx.Ok(new { message = "應付憑單已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 付款 Outcomes ──────────────────────────────────────────────────────────────

    // GET /admin/outcomes?page=1&pageSize=20
    public async Task<IActionResult> ListOutcomes(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        var (page, pageSize, offset) = Paging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Outcomes");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);
        var rows = await conn.QueryAsync(@"
SELECT oc.outcomeid, oc.outcomecode, oc.outcomedate, oc.amount, oc.note,
       oc.expenditureid, e.expenditurecode
FROM Outcomes oc JOIN Expenditures e ON e.expenditureid=oc.expenditureid
ORDER BY oc.outcomedate DESC, oc.outcomecode DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            outcomeId = r.outcomeid,
            outcomeCode = r.outcomecode,
            outcomeDate = r.outcomedate,
            r.amount,
            r.note,
            expenditureId = r.expenditureid,
            expenditureCode = r.expenditurecode,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // POST /admin/outcomes — 付款
    public async Task<IActionResult> CreateOutcome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateOutcomeRequest>();
        if (body is null || body.ExpenditureId == Guid.Empty) return ctx.BadRequest("缺少 expenditureId。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var exp = await conn.QuerySingleOrDefaultAsync<ExpenditureRow>(
                "SELECT expenditureid, status FROM Expenditures WHERE expenditureid=@id",
                new { id = body.ExpenditureId }, tx);
            if (exp is null) { tx.Rollback(); return ctx.NotFound("找不到應付憑單"); }
            if (exp.status == 2) { tx.Rollback(); return ctx.UnprocessableEntity("此應付憑單已全額付款。"); }

            var outCode = await _codes.NextAsync(CodeKind.Outcome, today, tx);
            await conn.ExecuteAsync(@"
INSERT INTO Outcomes (outcomeid, expenditureid, outcomecode, outcomedate, amount, note, createdate)
VALUES (NEWID(), @expId, @outCode, @date, @amount, @note, @now)",
                new { expId = body.ExpenditureId, outCode,
                      date = body.OutcomeDate ?? now, amount = body.Amount, note = body.Note, now }, tx);

            var newStatus = await RecalcExpenditureStatusAsync(conn, tx, body.ExpenditureId);
            tx.Commit();
            return ctx.Created(new { outcomeCode = outCode, newStatus });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/outcomes/{id} — 編輯付款，重算憑單狀態
    public async Task<IActionResult> UpdateOutcome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var oid)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateOutcomeRequest>();
        if (body is null) return ctx.BadRequest("請求內容不正確。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var expId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT expenditureid FROM Outcomes WHERE outcomeid=@oid", new { oid }, tx);
            if (expId is null) { tx.Rollback(); return ctx.NotFound("找不到付款記錄。"); }

            await conn.ExecuteAsync(
                "UPDATE Outcomes SET outcomedate=@date, amount=@amount, note=@note WHERE outcomeid=@oid",
                new { oid, date = body.OutcomeDate, amount = body.Amount, note = body.Note }, tx);

            await RecalcExpenditureStatusAsync(conn, tx, expId.Value);
            tx.Commit();
            return ctx.Ok(new { message = "已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/outcomes/{id}
    public async Task<IActionResult> DeleteOutcome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var oid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        var expId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT expenditureid FROM Outcomes WHERE outcomeid=@oid", new { oid });
        if (expId is null) return ctx.NotFound("找不到付款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync("DELETE FROM Outcomes WHERE outcomeid=@oid", new { oid }, tx);
            await RecalcExpenditureStatusAsync(conn, tx, expId.Value);
            tx.Commit();
            return ctx.Ok(new { message = "付款記錄已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // 重算應付憑單狀態：0 未付款 / 1 部分付款 / 2 已付款。回傳新狀態。
    private static async Task<int> RecalcExpenditureStatusAsync(SqlConnection conn, SqlTransaction tx, Guid expId)
    {
        var totalAmount = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(SUM(price),0) FROM Expendituredetails WHERE expenditureid=@id", new { id = expId }, tx);
        var paidAmount = await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(SUM(amount),0) FROM Outcomes WHERE expenditureid=@id", new { id = expId }, tx);
        var newStatus = totalAmount > 0 && paidAmount >= totalAmount ? 2 : (paidAmount > 0 ? 1 : 0);
        await conn.ExecuteAsync("UPDATE Expenditures SET status=@s WHERE expenditureid=@id",
            new { s = newStatus, id = expId }, tx);
        return newStatus;
    }

    // ── 退款 Refounds ──────────────────────────────────────────────────────────────

    // GET /admin/refounds?page=1&pageSize=20
    public async Task<IActionResult> ListRefounds(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        var (page, pageSize, offset) = Paging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Refounds");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);
        var rows = await conn.QueryAsync(@"
SELECT rf.refoundid, rf.refoundcode, rf.refounddate, rf.amount, rf.note,
       m.name AS memberName, r.returncode, o.ordercode
FROM Refounds rf
JOIN Members m ON m.memberid=rf.memberid
JOIN Returns r ON r.returnid=rf.returnid
JOIN Orders o ON o.orderid=r.orderid
ORDER BY rf.refounddate DESC, rf.refoundcode DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            refoundId = r.refoundid,
            refoundCode = r.refoundcode,
            refoundDate = r.refounddate,
            r.amount,
            r.note,
            memberName = r.memberName,
            returnCode = r.returncode,
            orderCode = r.ordercode,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/refounds/refundable-members — 有待退款退貨單的會員
    public async Task<IActionResult> ListRefundableMembers(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT DISTINCT m.memberid, m.name
FROM Returns r JOIN Members m ON m.memberid=r.memberid
WHERE r.refundstatus = 0
ORDER BY m.name");
        var items = rows.Select(r => (object)new { memberId = r.memberid, memberName = r.name }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/refounds/refundable-returns?memberId= — 某會員待退款的退貨單
    public async Task<IActionResult> ListRefundableReturns(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.Request.Query["memberId"], out var mid)) return ctx.BadRequest("缺少 memberId。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT r.returnid, r.returncode, r.returndate, o.ordercode, o.total AS orderTotal
FROM Returns r JOIN Orders o ON o.orderid=r.orderid
WHERE r.memberid=@mid AND r.refundstatus = 0
ORDER BY r.returndate DESC", new { mid });
        var items = rows.Select(r => (object)new
        {
            returnId = r.returnid,
            returnCode = r.returncode,
            returnDate = r.returndate,
            orderCode = r.ordercode,
            orderTotal = r.orderTotal,
        }).ToList();
        return ctx.Ok(items);
    }

    // POST /admin/refounds — 退款
    public async Task<IActionResult> CreateRefound(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateRefoundRequest>();
        if (body is null || body.ReturnId == Guid.Empty) return ctx.BadRequest("缺少 returnId。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var ret = await conn.QuerySingleOrDefaultAsync<ReturnRow>(
                "SELECT returnid, memberid, orderid FROM Returns WHERE returnid=@id",
                new { id = body.ReturnId }, tx);
            if (ret is null) { tx.Rollback(); return ctx.NotFound("找不到退貨單"); }

            var refoundCode = await _codes.NextAsync(CodeKind.Refound, today, tx);
            var refoundId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Refounds (refoundid, memberid, returnid, refoundcode, refounddate, amount, note, createdate)
VALUES (@refoundId, @memberId, @returnId, @refoundCode, @date, @amount, @note, @now)",
                new { refoundId, memberId = ret.memberid, returnId = body.ReturnId,
                      refoundCode, date = body.RefoundDate ?? now, amount = body.Amount, note = body.Note, now }, tx);

            // 標記退貨單已退款（refunddate 為 DateOnly），訂單 paystatus = 退款(2)
            await conn.ExecuteAsync(
                "UPDATE Returns SET refundstatus=1, refunddate=@d WHERE returnid=@id",
                new { id = body.ReturnId, d = today }, tx);
            await conn.ExecuteAsync(
                "UPDATE Orders SET paystatus=2 WHERE orderid=@id", new { id = ret.orderid }, tx);

            tx.Commit();
            return ctx.Created(new { refoundId, refoundCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/refounds/{id} — 編輯退款（金額/日期/備註）
    public async Task<IActionResult> UpdateRefound(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateRefoundRequest>();
        if (body is null) return ctx.BadRequest("請求內容不正確。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var ref0 = await conn.QuerySingleOrDefaultAsync<RefoundRow>(
                "SELECT refoundid, returnid FROM Refounds WHERE refoundid=@id", new { id }, tx);
            if (ref0 is null) { tx.Rollback(); return ctx.NotFound("找不到退款記錄。"); }

            await conn.ExecuteAsync(
                "UPDATE Refounds SET refounddate=@date, amount=@amount, note=@note WHERE refoundid=@id",
                new { id, date = body.RefoundDate, amount = body.Amount, note = body.Note }, tx);

            // 同步退貨單退款日（DateOnly）
            await conn.ExecuteAsync(
                "UPDATE Returns SET refunddate=@d WHERE returnid=@rid",
                new { d = DateOnly.FromDateTime(body.RefoundDate), rid = ref0.returnid }, tx);

            tx.Commit();
            return ctx.Ok(new { message = "已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/refounds/{id} — 刪除退款，還原退貨/訂單狀態
    public async Task<IActionResult> DeleteRefound(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        var ref0 = await conn.QuerySingleOrDefaultAsync<RefoundRow>(
            "SELECT refoundid, returnid FROM Refounds WHERE refoundid=@id", new { id });
        if (ref0 is null) return ctx.NotFound("找不到退款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var orderId = await conn.ExecuteScalarAsync<Guid?>(
                "SELECT orderid FROM Returns WHERE returnid=@rid", new { rid = ref0.returnid }, tx);

            await conn.ExecuteAsync(
                "UPDATE Returns SET refundstatus=0, refunddate=NULL WHERE returnid=@rid",
                new { rid = ref0.returnid }, tx);
            if (orderId is not null)
                await conn.ExecuteAsync(
                    "UPDATE Orders SET paystatus=1 WHERE orderid=@oid", new { oid = orderId }, tx);

            await conn.ExecuteAsync("DELETE FROM Refounds WHERE refoundid=@id", new { id }, tx);
            tx.Commit();
            return ctx.Ok(new { message = "退款記錄已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 請款 AR Invoices（內部應收） ───────────────────────────────────────────────

    // GET /admin/ar-invoices?page=1&pageSize=20
    public async Task<IActionResult> ListArInvoices(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        var (page, pageSize, offset) = Paging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Invoices");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);
        var rows = await conn.QueryAsync(@"
SELECT i.invoiceid, i.invoicecode, i.requestdate, i.incomeid, i.note,
       m.name AS memberName,
       ISNULL((SELECT SUM(ISNULL(id2.price,0)+ISNULL(id2.tax,0)) FROM Invoicedetails id2 WHERE id2.invoiceid=i.invoiceid),0) AS totalPrice
FROM Invoices i JOIN Members m ON m.memberid=i.memberid
ORDER BY i.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            invoiceId = r.invoiceid,
            invoiceCode = r.invoicecode,
            requestDate = r.requestdate,
            incomeId = r.incomeid,
            r.note,
            memberName = r.memberName,
            totalPrice = r.totalPrice,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/ar-invoices/billable-members — 有未請款訂單的會員
    public async Task<IActionResult> ListBillableMembers(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT DISTINCT m.memberid, m.name
FROM Orders o JOIN Members m ON m.memberid=o.memberid
WHERE o.paystatus = 0
  AND NOT EXISTS (SELECT 1 FROM Invoicedetails id2 WHERE id2.orderid=o.orderid)
ORDER BY m.name");
        var items = rows.Select(r => (object)new { memberId = r.memberid, memberName = r.name }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/ar-invoices/billable-orders?memberId= — 某會員可請款訂單
    public async Task<IActionResult> ListBillableOrders(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.Request.Query["memberId"], out var mid)) return ctx.BadRequest("缺少 memberId。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT o.orderid, o.ordercode, o.orderdate, o.total, o.freight, ISNULL(o.discount,0) AS discount
FROM Orders o
WHERE o.memberid=@mid AND o.paystatus = 0
  AND NOT EXISTS (SELECT 1 FROM Invoicedetails id2 WHERE id2.orderid=o.orderid)
ORDER BY o.orderdate DESC", new { mid });
        var items = rows.Select(r =>
        {
            int payable = (int)r.total + (int)r.freight - (int)r.discount;
            return (object)new
            {
                orderId = r.orderid,
                orderCode = r.ordercode,
                orderDate = r.orderdate,
                r.total,
                r.freight,
                r.discount,
                payable,
            };
        }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/ar-invoices/{id}
    public async Task<IActionResult> DetailArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var iid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var multi = await conn.QueryMultipleAsync(@"
SELECT i.invoiceid, i.invoicecode, i.requestdate, i.incomeid, i.note, i.memberid,
       m.name AS memberName, m.mobile AS memberMobile, m.address AS memberAddress
FROM Invoices i JOIN Members m ON m.memberid=i.memberid WHERE i.invoiceid=@iid;
SELECT id2.invoicedetailid, id2.orderid, id2.price, id2.tax, id2.note,
       o.ordercode, o.orderdate, o.invoicecode AS orderInvoiceCode
FROM Invoicedetails id2 LEFT JOIN Orders o ON o.orderid=id2.orderid WHERE id2.invoiceid=@iid;",
            new { iid });

        var h = await multi.ReadSingleOrDefaultAsync();
        if (h is null) return ctx.NotFound("找不到請款單");
        var detailRows = (await multi.ReadAsync()).ToList();

        var header = new
        {
            invoiceId = h.invoiceid,
            invoiceCode = h.invoicecode,
            requestDate = h.requestdate,
            incomeId = h.incomeid,
            h.note,
            memberId = h.memberid,
            memberName = h.memberName,
            memberMobile = h.memberMobile,
            memberAddress = h.memberAddress,
        };
        var details = detailRows.Select(r => (object)new
        {
            invoiceDetailId = r.invoicedetailid,
            orderId = r.orderid,
            orderCode = r.ordercode,
            orderDate = r.orderdate,
            orderInvoiceCode = r.orderInvoiceCode,
            r.price,
            r.tax,
            r.note,
        }).ToList();
        return ctx.Ok(new { invoice = header, details });
    }

    // POST /admin/ar-invoices — 以多筆訂單建立請款單
    public async Task<IActionResult> CreateArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateArInvoiceRequest>();
        if (body is null || body.OrderIds is null || body.OrderIds.Count == 0)
            return ctx.BadRequest("需要至少一筆訂單 ID（orderIds）。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var orders = (await conn.QueryAsync<OrderForInvoice>(
                "SELECT orderid, memberid, total, freight, ISNULL(discount,0) AS discount FROM Orders WHERE orderid IN @ids",
                new { ids = body.OrderIds }, tx)).ToList();

            if (orders.Count != body.OrderIds.Count) { tx.Rollback(); return ctx.BadRequest("部分訂單不存在。"); }
            var memberIds = orders.Select(o => o.memberid).Distinct().ToList();
            if (memberIds.Count > 1) { tx.Rollback(); return ctx.BadRequest("所有訂單必須屬於同一會員。"); }

            // 防止重複請款
            var already = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Invoicedetails WHERE orderid IN @ids", new { ids = body.OrderIds }, tx);
            if (already > 0) { tx.Rollback(); return ctx.UnprocessableEntity("部分訂單已建立請款單。"); }

            var invoiceCode = await _codes.NextAsync(CodeKind.Invoice, today, tx);
            var invoiceId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Invoices (invoiceid, invoicecode, memberid, requestdate, note, createdate)
VALUES (@invoiceId, @invoiceCode, @memberid, @today, @note, @now)",
                new { invoiceId, invoiceCode, memberid = memberIds[0], today, note = body.Note, now }, tx);

            foreach (var o in orders)
            {
                var netAmt = o.total + o.freight - o.discount;
                var tax = TaiwanVat.TaxOfInclusive(netAmt);
                await conn.ExecuteAsync(@"
INSERT INTO Invoicedetails (invoicedetailid, invoiceid, orderid, price, tax)
VALUES (NEWID(), @invoiceId, @orderid, @price, @tax)",
                    new { invoiceId, orderid = o.orderid, price = netAmt - tax, tax }, tx);
            }

            tx.Commit();
            return ctx.Created(new { invoiceId, invoiceCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/ar-invoices/{id} — 編輯請款單表頭（requestdate / note）
    public async Task<IActionResult> UpdateArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var iid)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateArInvoiceRequest>();
        if (body is null) return ctx.BadRequest("請求內容不正確。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var incomeId = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT incomeid FROM Invoices WHERE invoiceid=@iid", new { iid });
        if (incomeId is null && !await ExistsAsync(conn, "Invoices", "invoiceid", iid))
            return ctx.NotFound("找不到請款單。");

        var rows = await conn.ExecuteAsync(
            "UPDATE Invoices SET requestdate=@date, note=@note WHERE invoiceid=@iid",
            new { iid, date = body.RequestDate, note = body.Note });
        if (rows == 0) return ctx.NotFound("找不到請款單。");
        return ctx.Ok(new { message = "已更新" });
    }

    // DELETE /admin/ar-invoices/{id}
    public async Task<IActionResult> DeleteArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var iid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        var inv = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT invoiceid, incomeid FROM Invoices WHERE invoiceid=@iid", new { iid });
        if (inv is null) return ctx.NotFound("找不到請款單。");
        if (inv.incomeid is not null) return ctx.UnprocessableEntity("此請款單已連結收款記錄，無法刪除。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync("DELETE FROM Invoicedetails WHERE invoiceid=@iid", new { iid }, tx);
            await conn.ExecuteAsync("DELETE FROM Invoices WHERE invoiceid=@iid", new { iid }, tx);
            tx.Commit();
            return ctx.Ok(new { message = "請款單已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 入帳 Incomes（收款） ───────────────────────────────────────────────────────

    // GET /admin/incomes?page=1&pageSize=20
    public async Task<IActionResult> ListIncomes(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        var (page, pageSize, offset) = Paging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Incomes");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);
        var rows = await conn.QueryAsync(@"
SELECT ic.incomeid, ic.incomecode, ic.incomedate, ic.amount, ic.fee, ic.note,
       m.name AS memberName
FROM Incomes ic JOIN Members m ON m.memberid=ic.memberid
ORDER BY ic.incomedate DESC, ic.incomecode DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var items = rows.Select(r => (object)new
        {
            incomeId = r.incomeid,
            incomeCode = r.incomecode,
            incomeDate = r.incomedate,
            r.amount,
            r.fee,
            r.note,
            memberName = r.memberName,
        }).ToList();
        return ctx.OkPaged(PaginatedResponse<object>.Create(items, total, page, pageSize));
    }

    // GET /admin/incomes/billable-members — 有未收款請款單的會員
    public async Task<IActionResult> ListIncomeBillableMembers(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT DISTINCT m.memberid, m.name
FROM Invoices i JOIN Members m ON m.memberid=i.memberid
WHERE i.incomeid IS NULL
ORDER BY m.name");
        var items = rows.Select(r => (object)new { memberId = r.memberid, memberName = r.name }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/incomes/billable-invoices?memberId= — 某會員未收款的請款單
    public async Task<IActionResult> ListBillableInvoices(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.Request.Query["memberId"], out var mid)) return ctx.BadRequest("缺少 memberId。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        var rows = await conn.QueryAsync(@"
SELECT i.invoiceid, i.invoicecode, i.requestdate,
       ISNULL((SELECT SUM(ISNULL(id2.price,0)+ISNULL(id2.tax,0)) FROM Invoicedetails id2 WHERE id2.invoiceid=i.invoiceid),0) AS totalPrice
FROM Invoices i
WHERE i.memberid=@mid AND i.incomeid IS NULL
ORDER BY i.requestdate DESC", new { mid });
        var items = rows.Select(r => (object)new
        {
            invoiceId = r.invoiceid,
            invoiceCode = r.invoicecode,
            requestDate = r.requestdate,
            totalPrice = r.totalPrice,
        }).ToList();
        return ctx.Ok(items);
    }

    // GET /admin/incomes/{id}
    public async Task<IActionResult> DetailIncome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var incId)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var multi = await conn.QueryMultipleAsync(@"
SELECT ic.incomeid, ic.incomecode, ic.incomedate, ic.amount, ic.fee, ic.note, ic.memberid, m.name AS memberName
FROM Incomes ic JOIN Members m ON m.memberid=ic.memberid WHERE ic.incomeid=@incId;
SELECT i.invoiceid, i.invoicecode, i.requestdate,
       ISNULL((SELECT SUM(ISNULL(id2.price,0)+ISNULL(id2.tax,0)) FROM Invoicedetails id2 WHERE id2.invoiceid=i.invoiceid),0) AS totalPrice
FROM Invoices i WHERE i.incomeid=@incId;", new { incId });

        var h = await multi.ReadSingleOrDefaultAsync();
        if (h is null) return ctx.NotFound("找不到收款記錄");
        var invRows = (await multi.ReadAsync()).ToList();

        var header = new
        {
            incomeId = h.incomeid,
            incomeCode = h.incomecode,
            incomeDate = h.incomedate,
            h.amount,
            h.fee,
            h.note,
            memberId = h.memberid,
            memberName = h.memberName,
        };
        var invoices = invRows.Select(r => (object)new
        {
            invoiceId = r.invoiceid,
            invoiceCode = r.invoicecode,
            requestDate = r.requestdate,
            totalPrice = r.totalPrice,
        }).ToList();
        return ctx.Ok(new { income = header, invoices });
    }

    // POST /admin/incomes — 建立收款並標記訂單已付款
    public async Task<IActionResult> CreateIncome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateIncomeRequest>();
        if (body is null || body.MemberId == Guid.Empty) return ctx.BadRequest("缺少 memberId。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var incomeCode = await _codes.NextAsync(CodeKind.Income, today, tx);
            var incomeId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Incomes (incomeid, memberid, incomecode, incomedate, amount, fee, note, createdate)
VALUES (@incomeId, @memberId, @incomeCode, @date, @amount, @fee, @note, @now)",
                new { incomeId, memberId = body.MemberId, incomeCode,
                      date = body.IncomeDate ?? now, amount = body.Amount,
                      fee = body.Fee ?? 0, note = body.Note, now }, tx);

            if (body.InvoiceIds is { Count: > 0 })
            {
                await conn.ExecuteAsync(
                    "UPDATE Invoices SET incomeid=@incomeId WHERE invoiceid IN @ids",
                    new { incomeId, ids = body.InvoiceIds }, tx);

                var orderIds = (await conn.QueryAsync<Guid>(
                    "SELECT orderid FROM Invoicedetails WHERE invoiceid IN @ids AND orderid IS NOT NULL",
                    new { ids = body.InvoiceIds }, tx)).ToList();

                if (orderIds.Count > 0)
                {
                    var payDate = DateOnly.FromDateTime((body.IncomeDate ?? now));
                    await conn.ExecuteAsync(
                        "UPDATE Orders SET paystatus=1, paydate=@payDate WHERE orderid IN @ids",
                        new { payDate, ids = orderIds }, tx);
                }
            }

            tx.Commit();
            return ctx.Created(new { incomeId, incomeCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // PUT /admin/incomes/{id} — 編輯收款（金額/手續費/日期/備註），同步連結訂單付款日
    public async Task<IActionResult> UpdateIncome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var incId)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpdateIncomeRequest>();
        if (body is null) return ctx.BadRequest("請求內容不正確。");
        if (body.Amount <= 0) return ctx.BadRequest("amount 必須大於 0。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Incomes WHERE incomeid=@incId", new { incId }, tx);
            if (exists == 0) { tx.Rollback(); return ctx.NotFound("找不到收款記錄。"); }

            await conn.ExecuteAsync(
                "UPDATE Incomes SET incomedate=@date, amount=@amount, fee=@fee, note=@note WHERE incomeid=@incId",
                new { incId, date = body.IncomeDate, amount = body.Amount, fee = body.Fee ?? 0, note = body.Note }, tx);

            // 同步連結訂單付款日（DateOnly）
            var orderIds = (await conn.QueryAsync<Guid>(
                @"SELECT id2.orderid FROM Invoicedetails id2 JOIN Invoices i ON i.invoiceid=id2.invoiceid
                  WHERE i.incomeid=@incId AND id2.orderid IS NOT NULL", new { incId }, tx)).ToList();
            if (orderIds.Count > 0)
                await conn.ExecuteAsync("UPDATE Orders SET paydate=@d WHERE orderid IN @ids",
                    new { d = DateOnly.FromDateTime(body.IncomeDate), ids = orderIds }, tx);

            tx.Commit();
            return ctx.Ok(new { message = "已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/incomes/{id}
    public async Task<IActionResult> DeleteIncome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var incId)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(Ct(ctx));
        var income = await conn.ExecuteScalarAsync<Guid?>(
            "SELECT incomeid FROM Incomes WHERE incomeid=@incId", new { incId });
        if (income is null) return ctx.NotFound("找不到收款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var orderIds = (await conn.QueryAsync<Guid>(
                @"SELECT id2.orderid FROM Invoicedetails id2 JOIN Invoices i ON i.invoiceid=id2.invoiceid
                  WHERE i.incomeid=@incId AND id2.orderid IS NOT NULL", new { incId }, tx)).ToList();

            if (orderIds.Count > 0)
                await conn.ExecuteAsync("UPDATE Orders SET paystatus=0, paydate=NULL WHERE orderid IN @ids",
                    new { ids = orderIds }, tx);

            await conn.ExecuteAsync("UPDATE Invoices SET incomeid=NULL WHERE incomeid=@incId", new { incId }, tx);
            await conn.ExecuteAsync("DELETE FROM Incomes WHERE incomeid=@incId", new { incId }, tx);
            tx.Commit();
            return ctx.Ok(new { message = "收款記錄已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    private static async Task<bool> ExistsAsync(IDbConnection conn, string table, string col, Guid id)
        => await conn.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM {table} WHERE {col}=@id", new { id }) > 0;

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ExpenditureRow(Guid expenditureid, int status);
    private sealed record OrderForInvoice(Guid orderid, Guid memberid, int total, int freight, int discount);
    private sealed record ReturnRow(Guid returnid, Guid memberid, Guid orderid);
    private sealed record RefoundRow(Guid refoundid, Guid returnid);

    private sealed record ExchangeRequest(string? Title, decimal Rate);
    private sealed record AccountingRequest(string? AccountingCode, string? Title);

    private sealed record CreateExpenditureRequest(
        Guid? SupplierId, DateOnly? ExpenditureDate, string? Note,
        List<ExpenditureLineRequest>? Lines);
    private sealed record ExpenditureLineRequest(Guid? AccountingId, int Price, string? Summary);
    private sealed record UpdateExpenditureRequest(
        DateOnly ExpenditureDate, string? Note, List<UpdateExpenditureLine>? Lines);
    private sealed record UpdateExpenditureLine(
        Guid? ExpenditureDetailId, Guid? AccountingId, int Price, string? Summary);

    private sealed record CreateOutcomeRequest(
        Guid ExpenditureId, int Amount, DateTime? OutcomeDate, string? Note);
    private sealed record UpdateOutcomeRequest(int Amount, DateTime OutcomeDate, string? Note);

    private sealed record CreateArInvoiceRequest(List<Guid>? OrderIds, string? Note);
    private sealed record UpdateArInvoiceRequest(DateTime RequestDate, string? Note);

    private sealed record CreateIncomeRequest(
        Guid MemberId, int Amount, int? Fee, DateTime? IncomeDate, string? Note,
        List<Guid>? InvoiceIds);
    private sealed record UpdateIncomeRequest(int Amount, int? Fee, DateTime IncomeDate, string? Note);

    private sealed record CreateRefoundRequest(Guid ReturnId, int Amount, DateTime? RefoundDate, string? Note);
    private sealed record UpdateRefoundRequest(int Amount, DateTime RefoundDate, string? Note);
}
