using TFoodies.Domain.Common;

namespace TFoodies.Application.Abstractions;

/// <summary>
/// 信用卡授權成功後的共用處理。供財金 WEBPOS 的「授權結果導回(/return)」與
/// 「主動通知(/notify)」兩條路徑、以及後台「標記已付款 / 線上刷卡返回」共用。
/// </summary>
public interface IPaymentCompletionService
{
    /// <summary>
    /// 冪等標記訂單為已付款、建立 Income、寄付款完成通知信、開立電子發票（即時，失敗留未開供後台補開）。
    /// </summary>
    /// <param name="orderCode">訂單編號（= 財金 lidm）。</param>
    /// <param name="lastPan4">信用卡末四碼（財金回傳 lastPan4，可為 null；後台標記已付款傳 null）。</param>
    /// <param name="txnRef">交易參考字串，寫入 Incomes.note 供稽核（例：authCode / xid）。</param>
    /// <param name="payDate">入帳日（可選，預設今日；後台標記已付款可指定）。</param>
    /// <returns>true=本次首次轉為已付款；false=訂單不存在或已付款（冪等跳過，不重複寄信/開票）。</returns>
    Task<bool> MarkPaidAsync(string orderCode, string? lastPan4, string txnRef, DateOnly? payDate = null, CancellationToken ct = default);

    /// <summary>
    /// 開立電子發票（ezPay 即時）並建立本地 Invoices/Invoicedetails。
    /// 可開立的前提：發票類型非「免開」，且狀態為「未開(0)」或「已作廢(2)」（後者＝作廢後重新開立，取得新發票號）。
    /// 冪等：以 <c>WHERE invoicestatus IN (0,2)</c> 護欄避免 return+notify 雙觸發重複建檔。
    /// 供 MarkPaidAsync 自動呼叫，亦供後台「補開發票 / 作廢後重新開立」端點單獨呼叫。
    /// </summary>
    /// <param name="orderCode">訂單編號。</param>
    /// <param name="incomeId">關聯的收入 ID（付款流程帶入；後台補開／重開時可為 null）。</param>
    Task<Result> IssueInvoiceAsync(string orderCode, Guid? incomeId = null, CancellationToken ct = default);

    /// <summary>
    /// 作廢電子發票（ezPay invoice/void）並把訂單標記為「已作廢(2)」。冪等：僅當訂單發票狀態為「已開(1)」時才作廢。
    /// 作廢後可再呼叫 <see cref="IssueInvoiceAsync"/> 重新開立（取得新發票號）。供後台訂單詳情頁「作廢發票」端點呼叫。
    /// </summary>
    /// <param name="orderCode">訂單編號。</param>
    /// <param name="reason">作廢原因（寫入 ezPay InvalidReason，例：退貨）。</param>
    Task<Result> VoidInvoiceAsync(string orderCode, string reason, CancellationToken ct = default);
}
