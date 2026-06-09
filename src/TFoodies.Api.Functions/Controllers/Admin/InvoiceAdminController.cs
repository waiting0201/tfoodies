using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台電子發票管理（移植 InvoiceMsController）。
///
///   GET  /admin/invoices                    — 發票列表（可依 invoicestatus 篩選）
///   PATCH /admin/invoices/{id}/void         — 作廢發票
///   PATCH /admin/invoices/{id}/allowance    — 開立折讓
/// </summary>
public sealed class InvoiceAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly IInvoiceService _invoices;

    public InvoiceAdminController(
        IAdminPermissionService perms, IDbConnectionFactory db, IInvoiceService invoices)
    {
        _perms = perms; _db = db; _invoices = invoices;
    }

    // GET /admin/invoices?page=1&pageSize=20&invoiceStatus=
    public async Task<IActionResult> ListInvoices(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InvoiceMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        int? status = int.TryParse(q["invoiceStatus"], out var s) ? s : null;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var where = status.HasValue ? "o.invoicestatus = @status" : "1=1";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var countParams = status.HasValue ? (object)new { status = status.Value } : new { };
        var total = await conn.ExecuteScalarAsync<int>($@"
SELECT COUNT(1)
FROM Invoices i
JOIN Members m ON m.memberid = i.memberid
LEFT JOIN Orders o ON o.invoicecode = i.invoicecode
WHERE {where}", countParams);

        var dp = new DynamicParameters(countParams);
        dp.Add("offset", offset);
        dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync($@"
SELECT i.invoiceid, i.invoicecode, i.invoicedate, m.name AS memberName, o.invoicestatus
FROM Invoices i
JOIN Members m ON m.memberid = i.memberid
LEFT JOIN Orders o ON o.invoicecode = i.invoicecode
WHERE {where}
ORDER BY i.invoicedate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // PATCH /admin/invoices/{id}/void
    public async Task<IActionResult> VoidInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InvoiceMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var invoiceId))
            return ctx.BadRequest("無效的 ID。");

        var body = await ctx.TryReadBodyAsync<VoidRequest>();
        var reason = body?.Reason ?? "作廢";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var invoice = await conn.QuerySingleOrDefaultAsync<InvoiceRow>(
            "SELECT invoiceid, invoicecode FROM Invoices WHERE invoiceid = @invoiceId",
            new { invoiceId });
        if (invoice is null) return ctx.NotFound("找不到發票");

        // VoidAsync exists on IInvoiceService — call it first, then mark status=2
        var voidResult = await _invoices.VoidAsync(invoice.invoicecode, reason,
            ctx.Request.HttpContext.RequestAborted);

        if (!voidResult.IsSuccess)
        {
            return ctx.UnprocessableEntity($"作廢發票失敗：{voidResult.Error.Message}");
        }

        await conn.ExecuteAsync(
            "UPDATE Orders SET invoicestatus = 2 WHERE invoicecode = @code",
            new { code = invoice.invoicecode });

        return ctx.Ok(new { invoiceCode = invoice.invoicecode, invoiceStatus = 2 });
    }

    // PATCH /admin/invoices/{id}/allowance
    public async Task<IActionResult> AllowanceInvoice(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "InvoiceMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var invoiceId))
            return ctx.BadRequest("無效的 ID。");

        var body = await ctx.TryReadBodyAsync<AllowanceRequest>();
        var reason = body?.Reason ?? "折讓";
        var amount = body?.Amount;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var invoice = await conn.QuerySingleOrDefaultAsync<InvoiceRow>(
            "SELECT invoiceid, invoicecode FROM Invoices WHERE invoiceid = @invoiceId",
            new { invoiceId });
        if (invoice is null) return ctx.NotFound("找不到發票");

        // Build a minimal AllowanceRequest — caller may pass partial amount
        int totalAmt = amount ?? await conn.ExecuteScalarAsync<int>(
            @"SELECT ISNULL(SUM(id2.price + id2.tax), 0)
              FROM Invoicedetails id2
              WHERE id2.invoiceid = @invoiceId",
            new { invoiceId });

        var allowanceReq = new global::TFoodies.Application.Abstractions.AllowanceRequest(
            InvoiceNo: invoice.invoicecode,
            MerchantOrderNo: invoice.invoicecode,
            TotalAmt: totalAmt,
            Items: new[]
            {
                new global::TFoodies.Application.Abstractions.InvoiceItem(
                    Name: reason, Count: 1, Unit: "式", Price: totalAmt, Amount: totalAmt)
            });

        var allowanceResult = await _invoices.AllowanceAsync(allowanceReq,
            ctx.Request.HttpContext.RequestAborted);

        if (!allowanceResult.IsSuccess)
        {
            return ctx.UnprocessableEntity($"開立折讓失敗：{allowanceResult.Error.Message}");
        }

        await conn.ExecuteAsync(
            "UPDATE Orders SET invoicestatus = 3 WHERE invoicecode = @code",
            new { code = invoice.invoicecode });

        return ctx.Ok(new
        {
            invoiceCode = invoice.invoicecode,
            invoiceStatus = 3,
            allowanceNo = allowanceResult.Value?.AllowanceNo
        });
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record InvoiceRow(Guid invoiceid, string invoicecode);
    private sealed record VoidRequest(string? Reason);
    private sealed record AllowanceRequest(int? Amount, string? Reason);
}
