namespace TFoodies.Infrastructure.Payments.LinePay;

/// <summary>
/// LINE Pay Online API v3 設定（直連自有商店，非透過代收業者）。
///
/// 設定鍵（前端不持有任何金流設定，付款網址一律由 API 產生）：
///   LinePay__Enabled       — 總開關；false 時前台不顯示 LINE Pay、後端端點一律拒絕
///   LinePay__ChannelId     — LINE Pay 商店的 Channel ID
///   LinePay__ChannelSecret — Channel Secret（機密，HMAC-SHA256 簽章金鑰）
///   LinePay__BaseUrl       — 沙箱/正式切換的唯一開關（對齊 Fisc__ActionUrl 的設計）
///
/// ⚠️ 回呼與回跳網址刻意不另設鍵，一律沿用 <see cref="Fisc.FiscOptions"/> 的
/// ApiBaseUrl / StoreSuccessUrl / AllowedStoreOrigins（那三個本質是站台層級設定，
/// 與金流廠商無關）。避免同義設定鍵在四層之間漂移，見 docs/12-payment-invoice-config.md。
/// </summary>
public sealed class LinePayOptions
{
    public const string SectionName = "LinePay";

    /// <summary>總開關。取得正式金鑰前可先關閉，避免顧客選到不能真付款的選項。</summary>
    public bool Enabled { get; set; }

    /// <summary>LINE Pay 商店 Channel ID（送 X-LINE-ChannelId 標頭）。</summary>
    public string ChannelId { get; set; } = "";

    /// <summary>Channel Secret。同時是 HMAC-SHA256 的金鑰與被簽訊息的前綴（見 LinePaySigner）。</summary>
    public string ChannelSecret { get; set; } = "";

    /// <summary>
    /// API 基底。沙箱：https://sandbox-api-pay.line.me　營運：https://api-pay.line.me
    /// 上正式只需改此值與金鑰，程式不動。
    /// </summary>
    public string BaseUrl { get; set; } = "https://sandbox-api-pay.line.me";

    /// <summary>幣別。台灣一律 TWD（整數金額，無小數）。</summary>
    public string Currency { get; set; } = "TWD";

    /// <summary>API 逾時秒數。</summary>
    public int TimeoutSeconds { get; set; } = 20;

    // ── 導出值 ──

    /// <summary>已啟用且設定齊備才可發起交易。</summary>
    public bool IsUsable =>
        Enabled
        && !string.IsNullOrWhiteSpace(ChannelId)
        && !string.IsNullOrWhiteSpace(ChannelSecret)
        && !string.IsNullOrWhiteSpace(BaseUrl);
}
