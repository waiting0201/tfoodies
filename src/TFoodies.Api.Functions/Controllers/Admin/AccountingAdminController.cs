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
/// 後台財務 AP/AR 管理（移植 AccountingMsController）。
///
///   ── 應付（AP）──
///   GET  /admin/expenditures              — 應付憑單列表
///   GET  /admin/expenditures/{id}         — 應付憑單明細
///   POST /admin/expenditures              — 新增手動應付憑單
///   POST /admin/outcomes                  — 對應付憑單付款
///
///   ── 應收（AR）──
///   GET  /admin/ar-invoices               — 應收發票列表（內部 AR，非電子發票）
///   POST /admin/ar-invoices               — 建立應收發票（包裝多筆訂單）
///   GET  /admin/incomes                   — 應收收款列表
///   POST /admin/incomes                   — 建立收款記錄（標記訂單已付款）
///   POST /admin/refounds                  — 退款記錄
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

    // ── 應付憑單（Expenditures） ───────────────────────────────────────────────────

    // GET /admin/expenditures?status=&page=1&pageSize=20
    public async Task<IActionResult> ListExpenditures(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? status = int.TryParse(q["status"], out var s) ? s : null;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var where = status.HasValue ? "e.status = @status" : "1=1";
        var p = status.HasValue ? (object)new { status = status.Value } : new { };

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM Expenditures e WHERE {where}", p);

        var dp = new DynamicParameters(p);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync($@"
SELECT e.expenditureid, e.expenditurecode, e.expendituredate, e.status, e.sourcetype,
       s.title AS supplierTitle,
       ISNULL((SELECT SUM(ed.price) FROM Expendituredetails ed WHERE ed.expenditureid=e.expenditureid),0) AS totalAmount,
       ISNULL((SELECT SUM(oc.amount) FROM Outcomes oc WHERE oc.expenditureid=e.expenditureid),0) AS paidAmount
FROM Expenditures e
LEFT JOIN Suppliers s ON s.supplierid=e.supplierid
WHERE {where}
ORDER BY e.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/expenditures/{id}
    public async Task<IActionResult> DetailExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eid)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var multi = await conn.QueryMultipleAsync(@"
SELECT e.*, s.title AS supplierTitle FROM Expenditures e
LEFT JOIN Suppliers s ON s.supplierid=e.supplierid WHERE e.expenditureid=@eid;
SELECT ed.*, a.code AS accountingCode FROM Expendituredetails ed
LEFT JOIN Accountings a ON a.accountingid=ed.accountingid WHERE ed.expenditureid=@eid;
SELECT oc.outcomeid, oc.outcomecode, oc.outcomedate, oc.amount, oc.note FROM Outcomes oc
WHERE oc.expenditureid=@eid ORDER BY oc.outcomedate;",
            new { eid });

        var header = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (header is null) return ctx.NotFound("找不到應付憑單");
        var details = (await multi.ReadAsync<dynamic>()).ToList();
        var outcomes = (await multi.ReadAsync<dynamic>()).ToList();
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
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

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var exp = await conn.QuerySingleOrDefaultAsync<ExpenditureRow>(
                "SELECT expenditureid, status FROM Expenditures WHERE expenditureid=@id",
                new { id = body.ExpenditureId }, tx);
            if (exp is null) return ctx.NotFound("找不到應付憑單");
            if (exp.status == 2) return ctx.UnprocessableEntity("此應付憑單已全額付款。");

            var outCode = await _codes.NextAsync(CodeKind.Outcome, today, tx);
            await conn.ExecuteAsync(@"
INSERT INTO Outcomes (outcomeid, expenditureid, outcomecode, outcomedate, amount, note, createdate)
VALUES (NEWID(), @expId, @outCode, @date, @amount, @note, @now)",
                new { expId = body.ExpenditureId, outCode,
                      date = body.OutcomeDate ?? now, amount = body.Amount, note = body.Note, now }, tx);

            // 重算應付狀態：totalSum vs paid
            var totalAmount = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(SUM(price),0) FROM Expendituredetails WHERE expenditureid=@id",
                new { id = body.ExpenditureId }, tx);
            var paidAmount = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(SUM(amount),0) FROM Outcomes WHERE expenditureid=@id",
                new { id = body.ExpenditureId }, tx);

            var newStatus = paidAmount >= totalAmount ? 2 : (paidAmount > 0 ? 1 : 0);
            await conn.ExecuteAsync(
                "UPDATE Expenditures SET status=@newStatus WHERE expenditureid=@id",
                new { newStatus, id = body.ExpenditureId }, tx);

            tx.Commit();
            return ctx.Created(new { outcomeCode = outCode, newStatus });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── 應收發票（AR Invoices — 內部帳） ──────────────────────────────────────────

    // GET /admin/ar-invoices?page=1&pageSize=20
    public async Task<IActionResult> ListArInvoices(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var page = Math.Max(1, int.TryParse(ctx.Request.Query["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(ctx.Request.Query["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Invoices");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync(@"
SELECT i.invoiceid, i.invoicecode, i.requestdate, i.incomeid,
       m.name AS memberName,
       ISNULL((SELECT SUM(id2.price) FROM Invoicedetails id2 WHERE id2.invoiceid=i.invoiceid),0) AS totalPrice
FROM Invoices i JOIN Members m ON m.memberid=i.memberid
ORDER BY i.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // POST /admin/ar-invoices — 以多筆訂單建立應收發票
    public async Task<IActionResult> CreateArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<CreateArInvoiceRequest>();
        if (body is null || body.OrderIds is null || body.OrderIds.Count == 0)
            return ctx.BadRequest("需要至少一筆訂單 ID（orderIds）。");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 驗證所有訂單屬於同一會員（簡化：取第一筆的 memberid）
            var orders = (await conn.QueryAsync<OrderForInvoice>(
                "SELECT orderid, memberid, total, freight, ISNULL(discount,0) AS discount FROM Orders WHERE orderid IN @ids",
                new { ids = body.OrderIds }, tx)).ToList();

            if (orders.Count != body.OrderIds.Count) return ctx.BadRequest("部分訂單不存在。");
            var memberIds = orders.Select(o => o.memberid).Distinct().ToList();
            if (memberIds.Count > 1) return ctx.BadRequest("所有訂單必須屬於同一會員。");

            var invoiceCode = await _codes.NextAsync(CodeKind.Invoice, today, tx);
            var invoiceId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Invoices (invoiceid, invoicecode, memberid, requestdate, createdate)
VALUES (@invoiceId, @invoiceCode, @memberid, @today, @now)",
                new { invoiceId, invoiceCode, memberid = memberIds[0], today, now }, tx);

            // 每筆訂單建一筆 Invoicedetail（VAT 5% 拆分：TaiwanVat.Tax）
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

    // ── 應收收款（Incomes） ────────────────────────────────────────────────────────

    // GET /admin/incomes?page=1&pageSize=20
    public async Task<IActionResult> ListIncomes(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var page = Math.Max(1, int.TryParse(ctx.Request.Query["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(ctx.Request.Query["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Incomes");

        var dp = new DynamicParameters();
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync(@"
SELECT ic.incomeid, ic.incomecode, ic.incomedate, ic.amount, ic.fee,
       m.name AS memberName
FROM Incomes ic JOIN Members m ON m.memberid=ic.memberid
ORDER BY ic.incomedate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
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

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
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

            // 連結發票並標記訂單已付款
            if (body.InvoiceIds is { Count: > 0 })
            {
                await conn.ExecuteAsync(
                    "UPDATE Invoices SET incomeid=@incomeId WHERE invoiceid IN @ids",
                    new { incomeId, ids = body.InvoiceIds }, tx);

                // 取得發票對應的所有訂單，標記已付款
                var orderIds = await conn.QueryAsync<Guid>(
                    "SELECT orderid FROM Invoicedetails WHERE invoiceid IN @ids AND orderid IS NOT NULL",
                    new { ids = body.InvoiceIds }, tx);

                if (orderIds.Any())
                {
                    var payDate = now.Date;
                    await conn.ExecuteAsync(
                        "UPDATE Orders SET paystatus=1, paydate=@payDate WHERE orderid IN @ids",
                        new { payDate, ids = orderIds.ToList() }, tx);
                }
            }

            tx.Commit();
            return ctx.Created(new { incomeId, incomeCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/expenditures/{id}
    public async Task<IActionResult> DeleteExpenditure(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var outcomeCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Outcomes WHERE expenditureid=@eid", new { eid });
        if (outcomeCount > 0) return ctx.UnprocessableEntity("此應付憑單已有付款記錄，無法刪除。請先刪除付款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync("DELETE FROM Expendituredetails WHERE expenditureid=@eid", new { eid }, tx);
            var rows = await conn.ExecuteAsync("DELETE FROM Expenditures WHERE expenditureid=@eid", new { eid }, tx);
            if (rows == 0) { tx.Rollback(); return ctx.NotFound("找不到應付憑單。"); }
            tx.Commit();
            return ctx.Ok(new { message = "應付憑單已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/outcomes/{id}
    public async Task<IActionResult> DeleteOutcome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var oid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var outcome = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT outcomeid, expenditureid FROM Outcomes WHERE outcomeid=@oid", new { oid });
        if (outcome is null) return ctx.NotFound("找不到付款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync("DELETE FROM Outcomes WHERE outcomeid=@oid", new { oid }, tx);

            // 重算應付狀態
            Guid expId = outcome.expenditureid;
            var totalAmount = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(SUM(price),0) FROM Expendituredetails WHERE expenditureid=@id",
                new { id = expId }, tx);
            var paidAmount = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(SUM(amount),0) FROM Outcomes WHERE expenditureid=@id",
                new { id = expId }, tx);
            var newStatus = paidAmount >= totalAmount && totalAmount > 0 ? 2 : (paidAmount > 0 ? 1 : 0);
            await conn.ExecuteAsync(
                "UPDATE Expenditures SET status=@newStatus WHERE expenditureid=@id",
                new { newStatus, id = expId }, tx);

            tx.Commit();
            return ctx.Ok(new { message = "付款記錄已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/ar-invoices/{id}
    public async Task<IActionResult> DeleteArInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var iid)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var inv = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT invoiceid, incomeid FROM Invoices WHERE invoiceid=@iid", new { iid });
        if (inv is null) return ctx.NotFound("找不到應收發票。");
        if (inv.incomeid is not null) return ctx.UnprocessableEntity("此發票已連結收款記錄，無法刪除。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync("DELETE FROM Invoicedetails WHERE invoiceid=@iid", new { iid }, tx);
            await conn.ExecuteAsync("DELETE FROM Invoices WHERE invoiceid=@iid", new { iid }, tx);
            tx.Commit();
            return ctx.Ok(new { message = "應收發票已刪除" });
        }
        catch { tx.Rollback(); throw; }
    }

    // DELETE /admin/incomes/{id}
    public async Task<IActionResult> DeleteIncome(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "AccountingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var incId)) return ctx.BadRequest("無效的 ID。");

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var income = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT incomeid FROM Incomes WHERE incomeid=@incId", new { incId });
        if (income is null) return ctx.NotFound("找不到收款記錄。");

        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 取得連結的發票及訂單，還原訂單付款狀態
            var orderIds = (await conn.QueryAsync<Guid>(
                "SELECT id2.orderid FROM Invoicedetails id2 JOIN Invoices i ON i.invoiceid=id2.invoiceid WHERE i.incomeid=@incId AND id2.orderid IS NOT NULL",
                new { incId }, tx)).ToList();

            if (orderIds.Count > 0)
                await conn.ExecuteAsync("UPDATE Orders SET paystatus=0, paydate=NULL WHERE orderid IN @ids",
                    new { ids = orderIds }, tx);

            await conn.ExecuteAsync("UPDATE Invoices SET incomeid=NULL WHERE incomeid=@incId",
                new { incId }, tx);

            await conn.ExecuteAsync("DELETE FROM Incomes WHERE incomeid=@incId", new { incId }, tx);
            tx.Commit();
            return ctx.Ok(new { message = "收款記錄已刪除" });
        }
        catch { tx.Rollback(); throw; }
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

        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // 驗證退貨單存在並取得 memberid
            var ret = await conn.QuerySingleOrDefaultAsync<ReturnRow>(
                "SELECT returnid, memberid, orderid FROM Returns WHERE returnid=@id",
                new { id = body.ReturnId }, tx);
            if (ret is null) return ctx.NotFound("找不到退貨單");

            var refoundCode = await _codes.NextAsync(CodeKind.Refound, today, tx);
            var refoundId = Guid.NewGuid();

            await conn.ExecuteAsync(@"
INSERT INTO Refounds (refoundid, memberid, returnid, refoundcode, refounddate, amount, note, createdate)
VALUES (@refoundId, @memberId, @returnId, @refoundCode, @date, @amount, @note, @now)",
                new { refoundId, memberId = ret.memberid, returnId = body.ReturnId,
                      refoundCode, date = now, amount = body.Amount, note = body.Note, now }, tx);

            // 標記退貨單退款狀態 + 訂單 paystatus = 退款
            await conn.ExecuteAsync(
                "UPDATE Returns SET refundstatus=1 WHERE returnid=@id", new { id = body.ReturnId }, tx);
            await conn.ExecuteAsync(
                "UPDATE Orders SET paystatus=2 WHERE orderid=@id", new { id = ret.orderid }, tx);

            tx.Commit();
            return ctx.Created(new { refoundId, refoundCode });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ExpenditureRow(Guid expenditureid, int status);
    private sealed record OrderForInvoice(Guid orderid, Guid memberid, int total, int freight, int discount);
    private sealed record ReturnRow(Guid returnid, Guid memberid, Guid orderid);

    private sealed record CreateExpenditureRequest(
        Guid? SupplierId, DateOnly? ExpenditureDate, string? Note,
        List<ExpenditureLineRequest>? Lines);
    private sealed record ExpenditureLineRequest(Guid? AccountingId, int Price, string? Summary);
    private sealed record CreateOutcomeRequest(
        Guid ExpenditureId, int Amount, DateTime? OutcomeDate, string? Note);
    private sealed record CreateArInvoiceRequest(List<Guid>? OrderIds);
    private sealed record CreateIncomeRequest(
        Guid MemberId, int Amount, int? Fee, DateTime? IncomeDate, string? Note,
        List<Guid>? InvoiceIds);
    private sealed record CreateRefoundRequest(Guid ReturnId, int Amount, string? Note);
}
