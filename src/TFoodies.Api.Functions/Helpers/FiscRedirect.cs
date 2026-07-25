using TFoodies.Infrastructure.Payments.Fisc;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// FISC 刷卡回跳網域的白名單解析（防 open redirect）。
///
/// 多網域服務時，發起刷卡的網頁把自己的 origin 帶進 AuthResURL 的 query，授權返回時據以
/// 同網域導回（避免把使用者甩到別網域、且追蹤用的 sessionStorage 跨域讀不到而漏單）。
/// 建立端與回呼端都必須驗證，否則 query 就成了任人塞的轉址跳板。
///
/// 由訂單刷卡（PaymentController）與收款連結（PaymentLinkController）共用。
/// </summary>
public static class FiscRedirect
{
    /// <summary>
    /// 從候選來源依序取第一個「正規化後落在 Fisc 白名單內」的 origin；都不合格則回空字串
    /// （呼叫端據此退回設定的固定網址）。
    /// </summary>
    public static string ResolveAllowedOrigin(FiscOptions fisc, params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            var o = FiscOptions.NormalizeOrigin(c);
            if (o.Length > 0 && fisc.AllowedStoreOriginSet.Contains(o)) return o;
        }
        return "";
    }
}
