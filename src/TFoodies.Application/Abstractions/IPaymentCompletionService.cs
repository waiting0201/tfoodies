namespace TFoodies.Application.Abstractions;

/// <summary>
/// 信用卡授權成功後的共用處理。供財金 WEBPOS 的「授權結果導回(/return)」與
/// 「主動通知(/notify)」兩條路徑共用，與金流商的回傳格式無關。
/// </summary>
public interface IPaymentCompletionService
{
    /// <summary>
    /// 冪等標記訂單為已付款、建立 Income、寄付款完成通知信、最大努力開立電子發票。
    /// </summary>
    /// <param name="orderCode">訂單編號（= 財金 lidm）。</param>
    /// <param name="lastPan4">信用卡末四碼（財金回傳 lastPan4，可為 null）。</param>
    /// <param name="txnRef">交易參考字串，寫入 Incomes.note 供稽核（例：authCode / xid）。</param>
    /// <returns>true=本次首次轉為已付款；false=訂單不存在或已付款（冪等跳過，不重複寄信/開票）。</returns>
    Task<bool> MarkPaidAsync(string orderCode, string? lastPan4, string txnRef, CancellationToken ct = default);
}
