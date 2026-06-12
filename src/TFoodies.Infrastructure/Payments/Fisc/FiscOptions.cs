namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 FISC FOCAS_WEBPOS 網路收單設定（對齊舊系統 + 技術說明手冊 v2.7）。
/// WEBPOS 為前端 HTML form POST 導向刷卡頁，基本交易不需加密金鑰，僅需商店代號。
/// 機密性低（商店代號本就會出現在前端 form），但仍由設定提供以利測試/正式切換。
/// </summary>
public sealed class FiscOptions
{
    public const string SectionName = "Fisc";

    /// <summary>
    /// 刷卡頁 form action。測試：https://www.focas-test.fisc.com.tw/FOCAS_WEBPOS/online/
    /// 營運：https://www.focas.fisc.com.tw/FOCAS_WEBPOS/online/
    /// </summary>
    public string ActionUrl { get; set; } = "https://www.focas-test.fisc.com.tw/FOCAS_WEBPOS/online/";

    /// <summary>收單銀行授權使用的特店代號（固定 15 位，由收單銀行提供）。</summary>
    public string MerchantID { get; set; } = "";

    /// <summary>收單銀行授權使用的機台代號（固定 8 位）。</summary>
    public string TerminalID { get; set; } = "";

    /// <summary>網站特店自訂代碼（最大 10 位，注意與 MerchantID 不同）。</summary>
    public string MerID { get; set; } = "";

    /// <summary>特店網站或公司名稱，僅供刷卡頁顯示。</summary>
    public string MerchantName { get; set; } = "TFoodies";

    /// <summary>授權結果回傳網址（AuthResURL）= 後端 /store/payment/return 端點完整網址。</summary>
    public string AuthResUrl { get; set; } = "";

    /// <summary>前台訂單結果頁完整網址，供 return 端點處理完成後 302 導回。</summary>
    public string StoreSuccessUrl { get; set; } = "";
}
