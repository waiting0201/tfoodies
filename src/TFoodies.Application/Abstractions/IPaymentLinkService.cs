using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// 收款連結：後台填金額產生一次性連結，客人開連結填收件資料後直接刷卡（FISC WEBPOS）。
///
/// 與訂單刷卡的差異：不綁會員、不建 Orders/Orderdetails、不寫 Incomes、不自動開發票
/// （Orders.memberid 與 Incomes.memberid 皆 NOT NULL + FK，付款連結沒有會員可綁；
/// 硬塞假會員會污染會員數與會計報表）。付款成功只寄通知信給營運人工入帳與開票。
/// </summary>
public interface IPaymentLinkService
{
    /// <summary>
    /// 後台建立收款連結。單號與連結網址由後端產生。
    /// <paramref name="payMethod"/> 為 <see cref="Domain.Enums.PayType"/> 編碼，
    /// 目前僅支援 1（信用卡 FISC）與 8（LINE Pay）。
    /// </summary>
    Task<PaymentLinkCreated> CreateAsync(
        string title, string? note, int amount, int? validDays, int payMethod, int adminId,
        CancellationToken ct = default);

    /// <summary>
    /// 後台列表（依建立時間新到舊）。<paramref name="status"/> 為 null 表示不篩選；
    /// <paramref name="isExpired"/> 用於把「未付款」再切成未逾期／已逾期兩籃（逾期是衍生狀態，
    /// 不是獨立的 status 值），兩者互斥且加總等於 status=0 的全部。
    /// </summary>
    Task<(IReadOnlyList<PaymentLinkRow> Items, int Total)> ListAsync(
        int? status, bool? isExpired, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 客人端依 token 取連結資訊。刻意只回公開欄位（不含內部備註、客人已填資料、交易識別、
    /// 建立者）——連結若被轉發，不應洩漏這些內容。查無資料回 null。
    /// </summary>
    Task<PaymentLinkPublic?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// 客人送出收件資料 → 寫入該筆連結 → 依該連結的付款方式回傳「FISC 刷卡 form 欄位」
    /// 或「LINE Pay 付款頁網址」。金額一律取自 DB，前端不傳金額。
    /// 狀態非「未付款」或已逾期則失敗。
    /// </summary>
    Task<Result<PaymentLinkCharge>> StartCheckoutAsync(
        string token, PaymentLinkCustomer customer, string? returnOrigin, CancellationToken ct = default);

    /// <summary>
    /// 刷卡授權成功後冪等標記已付款並寄出通知信。
    /// 回傳 true 表示「本次首度轉為已付款」（return 與 notify 雙觸發時只會寄一封信）。
    /// </summary>
    Task<bool> MarkPaidAsync(string code, string? lastPan4, string txnRef, CancellationToken ct = default);

    /// <summary>
    /// LINE Pay 付款完成回跳：以收款單號取回 DB 金額 → 向 LINE Pay 請款確認 → 冪等標記已付款。
    /// 金額不由回跳參數決定，一律取自 DB。
    /// </summary>
    Task<Result> CompleteLinePayAsync(string code, string transactionId, CancellationToken ct = default);

    /// <summary>後台手動標記已付款（FISC 未回呼而實際已入帳時的補救）。</summary>
    Task<Result> MarkPaidManuallyAsync(Guid id, int adminId, CancellationToken ct = default);

    /// <summary>後台作廢未付款的連結。</summary>
    Task<Result> CancelAsync(Guid id, CancellationToken ct = default);
}

/// <summary>建立結果。<paramref name="Url"/> 為可直接交給客人的完整網址。</summary>
public sealed record PaymentLinkCreated(Guid Id, string Code, string Token, string Url);

/// <summary>客人於付款頁填寫的收件資料。</summary>
public sealed record PaymentLinkCustomer(string Name, string Mobile, int ZipcodeId, string Address);

/// <summary>客人端可見的連結資訊（公開欄位）。<paramref name="PayMethod"/> 供付款頁顯示付款方式。</summary>
public sealed record PaymentLinkPublic(
    string Code, string Title, int Amount, int Status, bool IsExpired, int PayMethod);

/// <summary>
/// 發起付款的指示，兩種形態擇一（由 <paramref name="PayMethod"/> 決定）：
///   1 信用卡 — 前端建隱藏欄位 auto-submit 至 <paramref name="ActionUrl"/>
///   8 LINE Pay — 前端整頁導向 <paramref name="RedirectUrl"/>
/// </summary>
public sealed record PaymentLinkCharge(
    int PayMethod,
    string? ActionUrl,
    IReadOnlyDictionary<string, string>? Fields,
    string? RedirectUrl)
{
    public static PaymentLinkCharge Form(string actionUrl, IReadOnlyDictionary<string, string> fields)
        => new((int)Domain.Enums.PayType.CreditCard, actionUrl, fields, null);

    public static PaymentLinkCharge Redirect(string url)
        => new((int)Domain.Enums.PayType.LinePay, null, null, url);
}

/// <summary>後台列表列。</summary>
public sealed record PaymentLinkRow(
    Guid Id,
    string Code,
    string Token,
    string Url,
    string Title,
    string? Note,
    int Amount,
    int Status,
    bool IsExpired,
    int PayMethod,
    string? CustomerName,
    string? CustomerMobile,
    string? CustomerAddress,
    string? LastPan4,
    DateTime? PayDate,
    DateTime? ExpireDate,
    DateTime CreateDate);
