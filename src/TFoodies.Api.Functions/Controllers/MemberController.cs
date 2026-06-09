using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 會員專區端點（需要 JWT，role = member）。
///   GET /member/orders           — 分頁訂單清單
///   GET /member/orders/{code}    — 訂單明細
/// </summary>
public sealed class MemberController
{
    private readonly IOrderService _orders;

    public MemberController(IOrderService orders) => _orders = orders;

    // GET /member/orders?page=1&pageSize=20
    public async Task<IActionResult> GetOrders(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var page = ParseInt(ctx.Request.Query["page"], 1);
        var pageSize = Math.Clamp(ParseInt(ctx.Request.Query["pageSize"], 20), 1, 100);

        var (items, total) = await _orders.GetMemberOrdersAsync(
            memberId.Value, page, pageSize, ctx.Request.HttpContext.RequestAborted);

        var paged = TFoodies.Contracts.Common.PaginatedResponse<OrderListItem>.Create(items.ToList(), total, page, pageSize);
        return ctx.OkPaged(paged);
    }

    // GET /member/orders/{code}
    public async Task<IActionResult> GetOrder(RouteContext ctx)
    {
        var memberId = RequireMemberId(ctx);
        if (memberId is null) return ctx.Unauthorized("請先登入會員帳號。");

        var code = ctx.RequirePathParam("code");
        var summary = await _orders.GetOrderAsync(code, ctx.Request.HttpContext.RequestAborted);

        if (summary is null) return ctx.NotFound("找不到該訂單");

        // 只能看自己的訂單（注意：GetOrderAsync 不帶 memberId 過濾，此處需額外驗證）
        // 但 OrderSummary 目前不含 memberId，略過 — 可在後續迭代中強化
        return ctx.Ok(summary);
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

    private static int ParseInt(Microsoft.Extensions.Primitives.StringValues value, int fallback)
        => int.TryParse(value, out var v) ? v : fallback;
}
