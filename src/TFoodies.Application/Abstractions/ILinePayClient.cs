using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// LINE Pay Online API v3 用戶端（直連，非透過代收業者）。
///
/// 兩段式交易：
///   1. <see cref="RequestAsync"/>  → 取得 paymentUrl 與 transactionId，導使用者至 LINE Pay 授權（此時尚未扣款）
///   2. <see cref="ConfirmAsync"/>  → 使用者授權後由 confirmUrl 回跳，後端呼叫 confirm 才真正請款
///
/// 未 confirm 就不會扣款（逾時自動失效），因此不需要像財金 WEBPOS 那樣的主動通知補償路徑。
/// </summary>
public interface ILinePayClient
{
    /// <summary>設定齊備且已啟用（未啟用時前台不得選 LINE Pay、端點一律拒絕）。</summary>
    bool IsEnabled { get; }

    /// <summary>建立交易並取得 LINE Pay 付款頁網址。</summary>
    Task<Result<LinePayReservation>> RequestAsync(LinePayReserveRequest request, CancellationToken ct = default);

    /// <summary>
    /// 請款確認。金額必須與 request 時相同（一律由 DB 重算，不信任前端）。
    /// 冪等：LINE Pay 對已完成的交易回 returnCode 1172，本方法視同成功，
    /// 讓 confirm 回跳被重放時不會誤判為失敗（實際的重複入帳防護在 MarkPaidAsync）。
    /// </summary>
    Task<Result<LinePayConfirmation>> ConfirmAsync(string transactionId, int amount, CancellationToken ct = default);
}

/// <summary>
/// 建立交易的輸入。<paramref name="OrderId"/> 為我方單號（訂單編號或收款單號 PL…），
/// LINE Pay 會在 confirmUrl 回跳時原樣帶回。<paramref name="Amount"/> 為應付總額（TWD 為整數）。
/// </summary>
public sealed record LinePayReserveRequest(
    string OrderId,
    int Amount,
    string ProductName,
    string ConfirmUrl,
    string CancelUrl);

/// <summary>建立交易的結果。<paramref name="PaymentUrlWeb"/> 供瀏覽器整頁導向。</summary>
public sealed record LinePayReservation(string TransactionId, string PaymentUrlWeb, string? PaymentUrlApp);

/// <summary>
/// 請款確認結果。<paramref name="AlreadyCompleted"/>=true 表示這筆交易先前已完成
/// （returnCode 1172），付款仍屬有效，只是本次不是首次完成。
/// </summary>
public sealed record LinePayConfirmation(string TransactionId, string? OrderId, bool AlreadyCompleted);
