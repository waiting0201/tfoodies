using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台刷卡收款連結（/admin/paymentlinks）。
///
/// 權限沿用 OrderMs：收款屬訂單/財務作業，且 Lims 模組樹為 DB 既有結構，新增模組名會讓
/// 除 itadmin 外所有帳號 403。本模組不在側欄（DB 無對應 Lim 列），從儀表板卡片進入。
/// </summary>
public sealed class PaymentLinkAdminController
{
    private const string Module = "OrderMs";
    private const int MaxAmount = 999_999_999;   // FISC purchAmt 為 N 且最長 10 位

    private readonly IPaymentLinkService _links;
    private readonly ILinePayClient _linePay;
    private readonly IAdminPermissionService _perms;

    public PaymentLinkAdminController(
        IPaymentLinkService links, ILinePayClient linePay, IAdminPermissionService perms)
    {
        _links = links;
        _linePay = linePay;
        _perms = perms;
    }

    // GET /admin/paymentlinks?page=&pageSize=&status=
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, Module, AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var ct = ctx.Request.HttpContext.RequestAborted;
        var q = ctx.Request.Query;

        var page = int.TryParse(q["page"], out var p) && p > 0 ? p : 1;
        var pageSize = int.TryParse(q["pageSize"], out var ps) && ps is > 0 and <= 100 ? ps : 20;
        int? status = int.TryParse(q["status"], out var s) ? s : null;
        // isExpired 把「未付款」再切成未逾期／已逾期兩籃（逾期非獨立 status 值）。
        bool? isExpired = bool.TryParse(q["isExpired"], out var e) ? e : null;

        var (items, total) = await _links.ListAsync(status, isExpired, page, pageSize, ct);
        return ctx.OkPaged(PaginatedResponse<PaymentLinkRow>.Create(items.ToList(), total, page, pageSize));
    }

    // POST /admin/paymentlinks
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, Module, AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var ct = ctx.Request.HttpContext.RequestAborted;
        var body = await ctx.TryReadBodyAsync<CreateRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var title = body.Title?.Trim() ?? "";
        if (title.Length is 0 or > 100) return ctx.BadRequest("請填寫收款項目（100 字以內）。");
        if (body.Amount is null or <= 0 or > MaxAmount) return ctx.BadRequest("金額必須為大於 0 的整數。");
        if (body.Note is { Length: > 500 }) return ctx.BadRequest("內部備註請控制在 500 字以內。");
        if (body.ValidDays is < 0 or > 365) return ctx.BadRequest("有效天數需介於 0（不限期）至 365 之間。");

        // 付款方式：未帶預設信用卡（沿用本功能上線時的既有行為）。
        var payMethod = body.PayMethod ?? (int)PayType.CreditCard;
        if (payMethod is not ((int)PayType.CreditCard or (int)PayType.LinePay))
            return ctx.BadRequest("付款方式僅支援信用卡或 LINE Pay。");
        if (payMethod == (int)PayType.LinePay && !_linePay.IsEnabled)
            return ctx.BadRequest("LINE Pay 目前未啟用，無法建立 LINE Pay 收款連結。");

        var created = await _links.CreateAsync(
            title, body.Note?.Trim(), body.Amount.Value, body.ValidDays, payMethod, guard.AdminId, ct);

        return ctx.Created(created);
    }

    // PATCH /admin/paymentlinks/{id}/cancel
    public async Task<IActionResult> Cancel(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, Module, AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id))
            return ctx.BadRequest("id 格式不正確。");

        var result = await _links.CancelAsync(id, ctx.Request.HttpContext.RequestAborted);
        return result.IsSuccess ? ctx.NoContent() : ctx.Conflict(result.Error.Message);
    }

    // PATCH /admin/paymentlinks/{id}/paid — FISC 未回呼但實際已入帳時的人工補救
    public async Task<IActionResult> MarkPaidManually(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, Module, AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id))
            return ctx.BadRequest("id 格式不正確。");

        var result = await _links.MarkPaidManuallyAsync(id, guard.AdminId, ctx.Request.HttpContext.RequestAborted);
        return result.IsSuccess ? ctx.NoContent() : ctx.Conflict(result.Error.Message);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ValidDays：null 或 0 代表不限期。
    /// PayMethod：PayType 編碼，1=信用卡（預設）、8=LINE Pay。
    /// </summary>
    private sealed record CreateRequest(
        string? Title, string? Note, int? Amount, int? ValidDays, int? PayMethod);
}
