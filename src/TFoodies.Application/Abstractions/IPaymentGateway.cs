using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// Credit-card acquiring via 財金網路收單共用系統 特店 API 2.0 (v1.2.8). Implementations
/// own the wire format (HMAC-SHA256 verifyCode + AES-GCM field crypto); callers work with
/// these clean DTOs. See plan §5.1/§5.2.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Create a purchase/authorization (optionally 3DS) for an order.</summary>
    Task<Result<PaymentInit>> CreateAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>Query an order's status (訂單查詢交易) — used by reconciliation to fill gaps.</summary>
    Task<Result<PaymentQuery>> QueryAsync(string merchantOrderNo, CancellationToken ct = default);

    /// <summary>Refund / 退貨交易.</summary>
    Task<Result<RefundResult>> RefundAsync(RefundRequest request, CancellationToken ct = default);

    /// <summary>Parse + verify a server-to-server NotifyURL callback; throws on bad signature.</summary>
    PaymentNotice ParseNotify(IReadOnlyDictionary<string, string> form);
}

/// <summary>amount is whole NTD; orderNumber = our ordercode.</summary>
public sealed record PaymentRequest(
    string OrderNumber,
    int Amount,
    string ReturnUrl,
    string NotifyUrl,
    bool Require3ds = true);

/// <summary>Where/how to send the shopper to complete payment (redirect/3DS or QR).</summary>
public sealed record PaymentInit(string OrderNumber, string RedirectUrl, IReadOnlyDictionary<string, string> FormFields);

public sealed record PaymentQuery(string OrderNumber, PaymentOutcome Outcome, int CumulativeRefund);

public sealed record RefundRequest(string OriginalOrderNumber, string RefundOrderNumber, int Amount, string CardNumber);

public sealed record RefundResult(bool Approved, string ResponseCode, string? Srrn);

/// <summary>Verified notify payload: who/what/how-much and the authoritative result.</summary>
public sealed record PaymentNotice(
    string OrderNumber,
    PaymentOutcome Outcome,
    string ResponseCode,
    string? Srrn,
    string? LastPan4,
    string TransactionId);

/// <summary>Maps 財金 order-status (tag10) / responseCode to a domain outcome.</summary>
public enum PaymentOutcome
{
    Authorized, // 9 授權成功
    Failed,     // X 失敗
    Refunded,   // 8 退貨
    Cancelled,  // 3 取消
    Unknown,
}
