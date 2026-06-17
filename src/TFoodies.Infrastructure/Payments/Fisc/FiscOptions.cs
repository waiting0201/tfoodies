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

    /// <summary>本 API 公開基底（含 /api）。前台/後台授權回呼網址由此導出。</summary>
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>前台訂單結果頁完整網址，供 return 端點處理完成後 302 導回。</summary>
    public string StoreSuccessUrl { get; set; } = "";

    /// <summary>後台訂單列表基底網址，供 return-admin 導回 {AdminSuccessUrl}/{orderCode}。</summary>
    public string AdminSuccessUrl { get; set; } = "";

    /// <summary>
    /// 允許作為前台刷卡回跳目標的 store 來源網域（origin = scheme://host[:port]）白名單，分號/逗號分隔。
    /// 多網域同時對外服務時，<c>/store/payment/return</c> 依使用者結帳所在網域（經此白名單驗證）動態導回，
    /// 避免跨域把使用者甩到別網域、且 purchase 追蹤的 sessionStorage 跨域讀不到而漏單。
    /// 防 open redirect：回跳 query 帶的 origin 不在此清單就退回 <see cref="StoreSuccessUrl"/>。
    /// 空＝不啟用動態導回（一律用 StoreSuccessUrl）。由 bicep 從 storeCustomDomains 自動導出。
    /// 例：<c>https://www.tfoodies.com;https://tfoodies.com;https://www.tfoodies.com.tw;https://tfoodies.com.tw</c>
    /// </summary>
    public string AllowedStoreOrigins { get; set; } = "";

    // ── 導出值（由設定組出，不由設定綁定）──

    /// <summary>白名單解析為正規化集合（scheme://host[:port]，小寫、無尾斜線）。</summary>
    public IReadOnlySet<string> AllowedStoreOriginSet =>
        AllowedStoreOrigins
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeOrigin)
            .Where(o => o.Length > 0)
            .ToHashSet();

    /// <summary><see cref="StoreSuccessUrl"/> 的路徑（如 /Order/Success），動態導回時與使用者網域組合。</summary>
    public string StoreSuccessPath =>
        Uri.TryCreate(StoreSuccessUrl, UriKind.Absolute, out var u) ? u.AbsolutePath : "/Order/Success";

    /// <summary>正規化來源網域為 scheme://host[:port]（小寫、無路徑/query/尾斜線）；非合法絕對 URL 回空字串。</summary>
    public static string NormalizeOrigin(string? origin)
        => Uri.TryCreate(origin?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")
            ? $"{u.Scheme}://{u.Authority}".ToLowerInvariant()
            : "";

    /// <summary>前台刷卡授權回呼網址 = {ApiBaseUrl}/store/payment/return。</summary>
    public string AuthResUrl => Combine(ApiBaseUrl, "store/payment/return");

    /// <summary>後台線上刷卡授權回呼網址 = {ApiBaseUrl}/store/payment/return-admin。</summary>
    public string AdminAuthResUrl => Combine(ApiBaseUrl, "store/payment/return-admin");

    private static string Combine(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path}";
}
