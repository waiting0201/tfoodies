using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 前台下單相關端點。
///   POST /store/orders          — 建立訂單（JWT 可選：登入會員帶 Bearer，匿名結帳不帶）
///   GET  /store/orders/{code}   — 查詢訂單（JWT 可選）
///   POST /store/discount/apply  — 折扣碼預覽（公開）
/// </summary>
public sealed class StoreOrderController
{
    private readonly IOrderService _orders;
    private readonly IDiscountService _discounts;

    public StoreOrderController(IOrderService orders, IDiscountService discounts)
    {
        _orders = orders;
        _discounts = discounts;
    }

    // POST /store/orders
    public async Task<IActionResult> PlaceOrder(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<PlaceOrderRequestDto>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var validation = body.Validate();
        if (validation is not null) return ctx.BadRequest(validation);

        var memberId = ExtractMemberId(ctx.CurrentUser);

        var request = body.ToRequest();
        var result = await _orders.PlaceOrderAsync(request, memberId, ctx.Request.HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            return result.Error.Code switch
            {
                "order.empty_cart" or "order.invalid_product" => ctx.BadRequest(result.Error.Message),
                "order.insufficient_stock" => ctx.UnprocessableEntity(result.Error.Message),
                _ => ctx.BadRequest(result.Error.Message),
            };
        }

        return ctx.Created(result.Value!);
    }

    // GET /store/orders/{code}
    public async Task<IActionResult> GetOrder(RouteContext ctx)
    {
        var code = ctx.RequirePathParam("code");
        var summary = await _orders.GetOrderAsync(code, ctx.Request.HttpContext.RequestAborted);
        if (summary is null) return ctx.NotFound("找不到該訂單");
        return ctx.Ok(summary);
    }

    // POST /store/discount/apply
    public async Task<IActionResult> ApplyDiscount(RouteContext ctx)
    {
        var body = await ctx.TryReadBodyAsync<DiscountApplyRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.DiscountCode))
            return ctx.BadRequest("缺少 discountCode 欄位。");

        var memberId = ExtractMemberId(ctx.CurrentUser);
        var subtotal = body.OrderSubtotal > 0 ? body.OrderSubtotal : 0;

        var result = await _discounts.ValidateAsync(
            body.DiscountCode, subtotal, memberId, ctx.Request.HttpContext.RequestAborted);

        if (!result.IsSuccess)
            return ctx.BadRequest(result.Error.Message);

        return ctx.Ok(new DiscountApplyResponse(result.Value!.DiscountCode, result.Value.DiscountAmount));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static Guid? ExtractMemberId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    // ── Request / Response DTOs ───────────────────────────────────────────────────

    private sealed record PlaceOrderRequestDto(
        List<CartLineDto>? Lines,
        string? BuyerName, string? BuyerMobile, string? BuyerEmail,
        int? BuyerZipcodeId, string? BuyerAddress, int? Gender,
        string? Password, DateOnly? Birthday,
        string? ReceiverName, string? ReceiverMobile,
        int? ReceiverZipcodeId, string? ReceiverAddress, int? ReceiverTime,
        int PayType, int InvoiceType,
        string? CompanyTitle, string? CompanyNumber,
        string? LoveCode, string? CarrierType, string? CarrierNum,
        string? DiscountCode, string? Remark)
    {
        public string? Validate()
        {
            if (Lines is null || Lines.Count == 0) return "購物車不能為空。";
            if (string.IsNullOrWhiteSpace(BuyerName)) return "缺少 buyerName 欄位。";
            if (string.IsNullOrWhiteSpace(BuyerMobile)) return "缺少 buyerMobile 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverName)) return "缺少 receiverName 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverMobile)) return "缺少 receiverMobile 欄位。";
            if (ReceiverZipcodeId is null) return "缺少 receiverZipcodeId 欄位。";
            if (string.IsNullOrWhiteSpace(ReceiverAddress)) return "缺少 receiverAddress 欄位。";
            return null;
        }

        public PlaceOrderRequest ToRequest() => new(
            Lines!.Select(l => new CartLineRequest(l.ProductId, l.Qty)).ToList(),
            BuyerName ?? string.Empty, BuyerMobile!, BuyerEmail,
            BuyerZipcodeId, BuyerAddress, Gender,
            Password, Birthday,
            ReceiverName!, ReceiverMobile!, ReceiverZipcodeId!.Value, ReceiverAddress!, ReceiverTime ?? 0,
            (Domain.Enums.PayType)PayType, (Domain.Enums.InvoiceType)InvoiceType,
            CompanyTitle, CompanyNumber, LoveCode, CarrierType, CarrierNum,
            DiscountCode, Remark);
    }

    private sealed record CartLineDto(Guid ProductId, int Qty);
    private sealed record DiscountApplyRequest(string? DiscountCode, int OrderSubtotal);
    private sealed record DiscountApplyResponse(string DiscountCode, int DiscountAmount);
}
