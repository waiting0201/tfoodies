using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Application.Abstractions;

// ── Request / Response DTOs ────────────────────────────────────────────────────

public sealed record PlaceOrderRequest(
    // 購物車（客戶端傳入，後端驗證實際售價）
    IReadOnlyList<CartLineRequest> Lines,
    // 訂購人資訊（匿名結帳時必填，登入會員可省略姓名/地址/zipcode）
    string BuyerName,
    string BuyerMobile,
    string? BuyerEmail,
    int? BuyerZipcodeId,
    string? BuyerAddress,
    int? Gender,
    string? Password,           // 匿名結帳時設定密碼（自動建會員）
    DateOnly? Birthday,
    // 收件資訊
    string ReceiverName,
    string ReceiverMobile,
    int ReceiverZipcodeId,
    string ReceiverAddress,
    int ReceiverTime,           // 0=不指定 1=上午 2=下午
    // 結帳參數
    PayType PayType,
    InvoiceType InvoiceType,
    string? CompanyTitle,
    string? CompanyNumber,
    string? LoveCode,
    string? CarrierType,
    string? CarrierNum,
    string? DiscountCode,
    string? Remark);

public sealed record CartLineRequest(Guid ProductId, int Qty);

public sealed record PlaceOrderResult(
    string OrderCode,
    string PayTypeKey,          // "credit" | "atmcode" | "delivery" | "nopay"
    string? AtmCode,            // ATM 付款時的虛擬帳號
    DateOnly? AtmExpiry,        // ATM 付款到期日
    int Total,
    int Freight,
    int Discount);

// ── Service interface ──────────────────────────────────────────────────────────

public interface IOrderService
{
    /// <summary>
    /// 建立訂單（含匿名建會員、折扣計算、ATM 碼產生）。
    /// 若 memberId 為 null，以 BuyerMobile + Password 找/建會員。
    /// </summary>
    Task<Result<PlaceOrderResult>> PlaceOrderAsync(
        PlaceOrderRequest request,
        Guid? memberId,
        CancellationToken ct = default);

    /// <summary>依 orderCode 取訂單摘要（會員或後台查詢）。</summary>
    Task<OrderSummary?> GetOrderAsync(string orderCode, CancellationToken ct = default);

    /// <summary>
    /// 該訂單是否屬於此會員。供「顧客自助重新付款」在帶了 JWT 時驗證歸屬——
    /// 訂單編號（O+日期+3 碼流水）可被猜出，不驗歸屬等於任何人都能以他人單號發起刷卡。
    /// 刻意不把 memberid 加進 OrderSummary：那會讓每個前台訂單回應都吐出會員 ID。
    /// </summary>
    Task<bool> IsOrderOwnedByMemberAsync(string orderCode, Guid memberId, CancellationToken ct = default);

    /// <summary>分頁取會員訂單清單。</summary>
    Task<(IReadOnlyList<OrderListItem> Items, int TotalCount)> GetMemberOrdersAsync(
        Guid memberId, int page, int pageSize, CancellationToken ct = default);
}

// ── Read model ─────────────────────────────────────────────────────────────────

public sealed record OrderSummary(
    Guid OrderId,
    string OrderCode,
    DateOnly OrderDate,
    int Total,
    int Freight,
    int Discount,
    PayType PayType,
    PayStatus PayStatus,
    DateOnly? PayDate,
    DeliverStatus DeliverStatus,
    DateOnly? DeliverDate,
    InvoiceType InvoiceType,
    string? AtmCode,
    DateOnly? AtmExpiry,
    string BuyerName,
    string BuyerMobile,
    string ReceiverName,
    string ReceiverMobile,
    string ReceiverAddress,
    string? Remark,
    IReadOnlyList<OrderLineItem> Lines);

public sealed record OrderLineItem(
    Guid ProductId, string ProductTitle, string ProductPhoto,
    int Qty, int Price, int Subtotal,
    string? Capacity, int IsGift);

public sealed record OrderListItem(
    Guid OrderId, string OrderCode, DateOnly OrderDate,
    int Total, int Freight, int Discount, PayStatus PayStatus, DeliverStatus DeliverStatus);
