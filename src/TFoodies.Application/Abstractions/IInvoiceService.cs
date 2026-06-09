using TFoodies.Domain.Common;
using TFoodies.Domain.Enums;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// Electronic invoicing via ezPay/NewebPay (EZP_INVI 1.2.2). Implementations own the wire
/// format (AES-256-CBC PostData_ + hex); callers work with these DTOs. See plan §5.1/§5.2.
/// </summary>
public interface IInvoiceService
{
    /// <summary>開立發票 (即時 / 等待觸發 / 預約自動).</summary>
    Task<Result<InvoiceResult>> IssueAsync(InvoiceRequest request, IssueMode mode, CancellationToken ct = default);

    /// <summary>開立折讓 (partial refund).</summary>
    Task<Result<InvoiceResult>> AllowanceAsync(AllowanceRequest request, CancellationToken ct = default);

    /// <summary>作廢發票 (full void / wrong issue).</summary>
    Task<Result<InvoiceResult>> VoidAsync(string invoiceNumber, string reason, CancellationToken ct = default);
}

/// <summary>ezPay Status: 1 即時開立 / 0 等待觸發 / 3 預約自動.</summary>
public enum IssueMode
{
    Immediate = 1,
    WaitForTrigger = 0,
    Scheduled = 3,
}

/// <summary>One invoice to issue. Amounts whole NTD; tax derived via Domain TaiwanVat.</summary>
public sealed record InvoiceRequest(
    string MerchantOrderNo,      // = ordercode
    InvoiceType Type,            // 二聯/三聯/捐贈… (Orders.invoicetype)
    string BuyerName,
    int TotalAmt,                // tax-inclusive
    IReadOnlyList<InvoiceItem> Items,
    string? BuyerUbn = null,     // 統編 (三聯/B2B)
    string? BuyerEmail = null,
    string? CarrierType = null,  // 0 手機條碼 / 1 自然人憑證 / 2 ezPay載具
    string? CarrierNum = null,
    string? LoveCode = null,     // 捐贈碼
    DateOnly? ScheduledDate = null);

public sealed record InvoiceItem(string Name, int Count, string Unit, int Price, int Amount);

public sealed record AllowanceRequest(
    string InvoiceNo,
    string MerchantOrderNo,
    int TotalAmt,
    IReadOnlyList<InvoiceItem> Items,
    string? BuyerEmail = null);

/// <summary>Normalised ezPay response.</summary>
public sealed record InvoiceResult(
    bool Success,
    string Status,
    string Message,
    string? InvoiceNumber = null,
    string? InvoiceTransNo = null,
    string? RandomNum = null,
    string? AllowanceNo = null,
    string? CheckCode = null);
