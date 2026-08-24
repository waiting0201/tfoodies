namespace TFoodies.Application.Abstractions;

/// <summary>
/// 刷卡授權結果紀錄（Paymentattempts）。
///
/// 存在的理由：顧客回報「刷卡沒有成功」時，財金回傳的 errcode / errDesc 是唯一能回答
/// 「為什麼」的資料。此前這兩欄被解析後直接丟棄、失敗也不記 log，客服與工程都查不到原因。
///
/// 呼叫端一律以 best-effort 使用：**寫紀錄失敗絕不可影響付款流程**（同 IssueInvoiceAsync 的原則）。
/// </summary>
public interface IPaymentAttemptLog
{
    /// <summary>寫入一筆授權回呼結果。實作內部吞掉例外並回傳 false，呼叫端不需 try/catch。</summary>
    Task<bool> RecordAsync(PaymentAttempt attempt, CancellationToken ct = default);

    /// <summary>取該單號最近的刷卡紀錄（新到舊），供後台訂單詳情顯示。</summary>
    Task<IReadOnlyList<PaymentAttempt>> GetByLidmAsync(string lidm, int take = 10, CancellationToken ct = default);
}

/// <summary>
/// 一次授權回呼的結果。<paramref name="Source"/> 為回呼來源
/// （return / return-admin / notify / return-paylink）。
/// ⚠️ 刻意不含卡號欄位：財金雖回傳遮罩卡號 pan，本系統只留末四碼。
/// </summary>
public sealed record PaymentAttempt(
    string Lidm,
    string Source,
    bool IsSuccess,
    string? Status = null,
    string? ErrCode = null,
    string? ErrDesc = null,
    string? AuthCode = null,
    string? Xid = null,
    string? LastPan4 = null,
    string? CardBrand = null,
    int? AuthAmt = null,
    string? Note = null,
    DateTime? CreateDate = null);
