namespace TFoodies.Infrastructure.Payments.Fisc;

/// <summary>
/// 財金 FISC FOCAS_WEBPOS 網路收單設定（對齊舊系統 + 技術說明手冊 v2.7）。
/// WEBPOS 為前端 HTML form POST 導向刷卡頁，基本交易不需加密金鑰，僅需商店代號。
///
/// 設定鍵（前端不持有任何 FISC 設定，一律由 API 產生刷卡欄位）：
///   Fisc__ActionUrl        — 財金刷卡頁 URL（測試/正式切換的唯一開關）
///   Fisc__MerchantID/TerminalID/MerID — 商店代號（測試與正式相同）
///   Fisc__ApiBaseUrl       — 本 API 公開基底（含 /api），授權回呼網址由此導出
///   Fisc__StoreSuccessUrl  — 前台結果頁完整網址
///   Fisc__AdminSuccessUrl  — 後台訂單頁基底網址
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

    /// <summary>
    /// 本 Function App 的公開 API 基底（含 /api）。後台線上刷卡的回呼網址（AdminAuthResUrl）由此導出。
    /// </summary>
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>
    /// 前台 store 網域基底（含 /api，例：https://www.tfoodies.com/api）。store 以反向代理把
    /// /api/store/payment/return 轉發到 Functions，使「刷卡頁網域 = AuthResURL 網域 = 財金登錄網域」
    /// （還原舊系統單體同網域送單的條件）。留空則 fallback 用 ApiBaseUrl。
    /// </summary>
    public string StoreApiBaseUrl { get; set; } = "";

    /// <summary>前台訂單結果頁完整網址，供 return 端點處理完成後 302 導回。</summary>
    public string StoreSuccessUrl { get; set; } = "";

    /// <summary>後台訂單列表基底網址，供 return-admin 導回 {AdminSuccessUrl}/{orderCode}。</summary>
    public string AdminSuccessUrl { get; set; } = "";

    // ── 導出值（由各自網域基底組出，不由設定綁定）──
    // 前台與後台刷卡頁分屬不同網域，AuthResURL 必須各自落在「發起刷卡頁的同網域」（財金以此檢核）。

    /// <summary>前台刷卡授權回呼網址 = {StoreApiBaseUrl 或 ApiBaseUrl}/store/payment/return（store 網域）。</summary>
    public string AuthResUrl => Combine(string.IsNullOrWhiteSpace(StoreApiBaseUrl) ? ApiBaseUrl : StoreApiBaseUrl, "store/payment/return");

    /// <summary>後台線上刷卡授權回呼網址 = {ApiBaseUrl}/store/payment/return-admin。</summary>
    public string AdminAuthResUrl => Combine(ApiBaseUrl, "store/payment/return-admin");

    private static string Combine(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path}";
}
